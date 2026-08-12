namespace KartInventoryService.Application.Common.Models;

/// <summary>Response shape for POST /inventory/{warehouseId}/{sku}/reconcile - surfaces the computed variance, not just the new quantity.</summary>
public sealed record StockReconciliationResultDto(string Sku, string WarehouseId, int PreviousQty, int CountedQty, int Variance);
