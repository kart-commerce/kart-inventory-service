using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// event-contract.md InventoryReserved (orderId, sku, qty) - raised only once the Reservation is
/// durably committed (Outbox pattern), never before, so Order cannot advance the saga on a
/// reservation that could still fail to persist.
/// </summary>
public sealed record InventoryReservedDomainEvent(
    Guid OrderId,
    string Sku,
    int Qty,
    DateTimeOffset OccurredAt) : IDomainEvent;
