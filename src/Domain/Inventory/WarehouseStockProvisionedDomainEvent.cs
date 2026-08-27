using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Raised by WarehouseStock.Provision - onboarding a brand-new (WarehouseId, Sku) row. Closes a
/// previously flagged gap (Provision's own doc comment: "never reachable through a public API,
/// only through a migration seed") once a real admin endpoint exists to call it.
/// </summary>
public sealed record WarehouseStockProvisionedDomainEvent(
    string WarehouseId,
    string Sku,
    int InitialQty,
    DateTimeOffset OccurredAt) : IDomainEvent;
