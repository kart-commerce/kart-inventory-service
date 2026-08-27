using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.UpdateReplenishmentThreshold;

/// <summary>Inventory &amp; Stock Management flow's "Low Stock Threshold" stage - admin-adjustable, previously settable only at Provision time.</summary>
public sealed record UpdateReplenishmentThresholdCommand(
    string WarehouseId,
    string Sku,
    int ReplenishmentThreshold,
    int TargetStockingLevel) : IRequest<Result<StockLevelDto>>;
