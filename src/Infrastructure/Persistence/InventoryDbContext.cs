using KartInventoryService.Domain.Inventory;
using KartInventoryService.Infrastructure.Messaging;
using KartInventoryService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace KartInventoryService.Infrastructure.Persistence;

public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<InventoryOutboxEvent> OutboxEvents => Set<InventoryOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WarehouseStockConfiguration());
        modelBuilder.ApplyConfiguration(new ReservationConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseAllocationConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryOutboxEventConfiguration());
    }

    /// <summary>
    /// Converts every tracked WarehouseStock/Reservation's pending domain events into
    /// inventory_outbox_events rows within this same SaveChanges call (design-decisions.md,
    /// "Event Publish Atomicity") - the write and "the event will eventually publish" commit
    /// atomically, never as a separate, unguarded publish step. Mirrors kart-category-service's
    /// CategoryDbContext.SaveChangesAsync override exactly.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var reservationsWithEvents = ChangeTracker.Entries<Reservation>()
            .Select(entry => entry.Entity)
            .Where(reservation => reservation.DomainEvents.Count > 0)
            .ToList();

        var stocksWithEvents = ChangeTracker.Entries<WarehouseStock>()
            .Select(entry => entry.Entity)
            .Where(stock => stock.DomainEvents.Count > 0)
            .ToList();

        foreach (var reservation in reservationsWithEvents)
        {
            foreach (var domainEvent in reservation.DomainEvents)
            {
                var outboxEvent = domainEvent switch
                {
                    InventoryReservedDomainEvent reserved => InventoryOutboxEvent.ForReserved(reserved, reservation.ReservationId, reservation.UpdatedBy),
                    InventoryReleasedDomainEvent released => InventoryOutboxEvent.ForReleased(released, reservation.ReservationId, reservation.UpdatedBy),
                    _ => throw new InvalidOperationException($"Unhandled Reservation domain event type '{domainEvent.GetType().Name}'."),
                };

                OutboxEvents.Add(outboxEvent);
            }
        }

        foreach (var stock in stocksWithEvents)
        {
            foreach (var domainEvent in stock.DomainEvents)
            {
                var outboxEvent = domainEvent switch
                {
                    InventoryReplenishedDomainEvent replenished => InventoryOutboxEvent.ForReplenished(replenished, $"{stock.WarehouseId}:{stock.Sku}", stock.UpdatedBy),
                    _ => throw new InvalidOperationException($"Unhandled WarehouseStock domain event type '{domainEvent.GetType().Name}'."),
                };

                OutboxEvents.Add(outboxEvent);
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var reservation in reservationsWithEvents)
        {
            reservation.ClearDomainEvents();
        }

        foreach (var stock in stocksWithEvents)
        {
            stock.ClearDomainEvents();
        }

        return result;
    }
}
