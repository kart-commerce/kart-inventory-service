using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Inventory &amp; Stock Management flow's "Reorder Alert" stage: raised whenever a write
/// (TryDebit/Replenish/Reconcile) leaves AvailableQty below ReplenishmentThreshold. Previously
/// this signal was a LogWarning only (never left this process) - promoting it to a real published
/// event lets procurement/admin tooling (or a future automated reorder trigger, per
/// AuthenticationExtensions' pre-existing "service:inventory-replenishment-trigger" role) actually
/// react to it instead of relying on someone grepping logs.
/// </summary>
public sealed record LowStockDetectedDomainEvent(
    string WarehouseId,
    string Sku,
    int AvailableQty,
    int Threshold,
    DateTimeOffset OccurredAt) : IDomainEvent;
