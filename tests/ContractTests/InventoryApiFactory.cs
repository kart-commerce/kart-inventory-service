using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.ContractTests.Fakes;
using KartInventoryService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KartInventoryService.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline (Program.cs unchanged), swapping PostgreSQL/Redis/
/// RabbitMQ for in-memory fakes and replacing JWT bearer auth with TestAuthenticationHandler -
/// asserts HTTP wire-shape only (status codes, JSON field names, RBAC gating), never touching a
/// real database/broker. Mirrors kart-category-service's ContractTests factory.
/// </summary>
public sealed class InventoryApiFactory : WebApplicationFactory<Program>
{
    public InMemoryWarehouseStockRepository StockRepository { get; } = new();

    public InMemoryReservationRepository ReservationRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // None of the hosted services Infrastructure registers (RabbitMQ topology/outbox
            // relay/order-events consumer, TTL sweep) can reach a real Postgres/RabbitMQ here.
            services.RemoveAll(typeof(IHostedService));

            services.RemoveAll(typeof(DbContextOptions<InventoryDbContext>));
            services.RemoveAll(typeof(InventoryDbContext));

            services.RemoveAll(typeof(IWarehouseStockRepository));
            services.AddSingleton<IWarehouseStockRepository>(StockRepository);

            services.RemoveAll(typeof(IReservationRepository));
            services.AddSingleton<IReservationRepository>(ReservationRepository);

            services.RemoveAll(typeof(IUnitOfWork));
            services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();

            services.RemoveAll(typeof(IStockCache));
            services.AddSingleton<IStockCache, NullStockCache>();

            services.RemoveAll(typeof(IOutboxEventWriter));
            services.AddSingleton<IOutboxEventWriter, NullOutboxEventWriter>();

            // Replaces the JWT bearer scheme registered by Program.cs's AddInventoryAuthentication -
            // this AddAuthentication(defaultScheme:) call runs after Program.cs's own, so it wins
            // for AuthenticationOptions.DefaultScheme/DefaultAuthenticateScheme. Authorization
            // policies (OrderServicePolicy/ReplenishPolicy) are untouched - they check the `roles`
            // claim regardless of which handler produced it.
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        });
    }
}
