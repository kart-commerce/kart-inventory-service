using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>event-contract.md InventoryReplenished (sku, qtyAdded, warehouseId).</summary>
public sealed record InventoryReplenishedDomainEvent(
    string Sku,
    int QtyAdded,
    string WarehouseId,
    DateTimeOffset OccurredAt) : IDomainEvent;
