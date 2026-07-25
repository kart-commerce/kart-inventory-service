using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Options;
using KartInventoryService.Infrastructure.BackgroundServices;
using KartInventoryService.Infrastructure.Caching;
using KartInventoryService.Infrastructure.Messaging;
using KartInventoryService.Infrastructure.Persistence;
using KartInventoryService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace KartInventoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InventoryOptions>(configuration.GetSection("Inventory"));

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("InventoryDatabase")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost:6379"));

        services.AddScoped<IWarehouseStockRepository, WarehouseStockRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IStockCache, RedisStockCache>();
        services.AddScoped<IOutboxEventWriter, OutboxEventWriter>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();

        // contracts/message-bus-manifest.json is the single source of truth for this service's
        // entire RabbitMQ topology - every exchange, queue, binding, dead-letter and retry-tier
        // name. Nothing messaging-related is hardcoded in C#: the manifest is loaded once here
        // and shared as a singleton; RabbitMqTopologyProvisioner scans it to declare the
        // topology. IConnectionFactory only builds config, it does not connect eagerly, so
        // registering it here is safe even if RabbitMQ is unreachable at startup -
        // RabbitMqTopologyStartupHostedService, OutboxRelayHostedService and
        // OrderEventsConsumerHostedService each own their own retrying connection.
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var manifestPath = Path.IsPathRooted(options.ManifestPath)
                ? options.ManifestPath
                : Path.Combine(AppContext.BaseDirectory, options.ManifestPath);
            return MessageBusManifestLoader.Load(manifestPath);
        });
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"] ?? "localhost",
            DispatchConsumersAsync = true,
        });
        services.AddHostedService<RabbitMqTopologyStartupHostedService>();
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<OrderEventsConsumerHostedService>();

        // INV-5: TTL sweep, entirely internal to this service (requirement-spec.md Decision 2).
        services.AddHostedService<SweepExpiredReservationsHostedService>();

        return services;
    }
}
