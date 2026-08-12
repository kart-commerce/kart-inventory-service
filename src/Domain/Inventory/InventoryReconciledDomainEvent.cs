using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Inventory &amp; Stock Management flow's "Stock Audit/Reconciliation" stage: raised when an
/// admin-submitted physical count (WarehouseStock.Reconcile) overwrites AvailableQty. Variance is
/// CountedQty - PreviousQty (positive = found more than the ledger showed, negative = shrinkage).
/// This is also this flow's "Update Qty" stage - the one place an operator can set stock to an
/// absolute value rather than debit/credit it.
/// </summary>
public sealed record InventoryReconciledDomainEvent(
    string WarehouseId,
    string Sku,
    int PreviousQty,
    int CountedQty,
    int Variance,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
