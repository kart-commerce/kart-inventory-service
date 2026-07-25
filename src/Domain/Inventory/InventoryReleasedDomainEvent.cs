using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// event-contract.md InventoryReleased (orderId, sku, qty) - the compensating counterpart to
/// InventoryReserved. Raised only on a genuine Reserved -> Released/Expired transition
/// (Reservation.Release's ReleaseOutcome.Released), never on an idempotent no-op against an
/// already-terminal reservation.
/// </summary>
public sealed record InventoryReleasedDomainEvent(
    Guid OrderId,
    string Sku,
    int Qty,
    DateTimeOffset OccurredAt) : IDomainEvent;
