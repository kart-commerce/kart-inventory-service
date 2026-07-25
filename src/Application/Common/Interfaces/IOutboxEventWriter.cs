namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>
/// Writes a single inventory_outbox_events row directly, for the one published event that is not
/// attached to any persisted aggregate: InventoryReservationFailed (a failed POST /inventory/reserve
/// never creates a Reservation, so there is no aggregate for IUnitOfWork's DomainEvents-scanning
/// SaveChanges override to pick up). Every other published event (InventoryReserved,
/// InventoryReleased, InventoryReplenished) is raised as a domain event on WarehouseStock/
/// Reservation and converted to an outbox row by that same SaveChanges override instead - this
/// interface exists only for the one event that isn't. Commits its own row independently (the
/// caller has typically just rolled back the failed reservation transaction).
/// </summary>
public interface IOutboxEventWriter
{
    Task EnqueueAsync(string eventType, string aggregateRef, object payload, string actingPrincipal, CancellationToken cancellationToken);
}
