using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.ReconcileStock;

/// <summary>Inventory &amp; Stock Management flow's "Stock Audit/Reconciliation" and "Update Qty" stages - overwrites AvailableQty to an admin-submitted physical count, recording the signed variance.</summary>
public sealed record ReconcileStockCommand(
    string WarehouseId,
    string Sku,
    int CountedQty,
    string Reason) : IRequest<Result<StockReconciliationResultDto>>;
