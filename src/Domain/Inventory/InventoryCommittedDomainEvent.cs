using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Inventory &amp; Stock Management flow's "Deduct (Order Confirmed)" stage: raised when a
/// Reservation transitions Reserved -&gt; Committed on consuming OrderConfirmed. No further
/// WarehouseStock debit happens here (the debit already happened at Reserve time) - this marks
/// the hold as permanent/no-longer-TTL-releasable, which is the real semantic of "deduct" given
/// this aggregate's reserve-debits-immediately design.
/// </summary>
public sealed record InventoryCommittedDomainEvent(
    Guid OrderId,
    string Sku,
    int Qty,
    DateTimeOffset OccurredAt) : IDomainEvent;
