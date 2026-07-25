using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KartInventoryService.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory used solely by `dotnet ef migrations add`/`database update` - reads
/// INVENTORY_DB_CONNECTION_STRING directly so it doesn't require the full Api host/JWT/RabbitMQ
/// configuration to be present (mirrors kart-identity-service's IdentityDbContextFactory).
/// </summary>
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("INVENTORY_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_inventory;Username=kart_inventory_service;Password=changeme";

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
