using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.ProvisionWarehouseStock;

/// <summary>
/// Inventory &amp; Stock Management flow: onboards a brand-new (WarehouseId, Sku) row - the
/// previously-flagged gap where WarehouseStock.Provision was reachable only via a migration seed.
/// AdminOnly-gated (master-data onboarding, not a routine replenishment).
/// </summary>
public sealed record ProvisionWarehouseStockCommand(
    string WarehouseId,
    string Sku,
    int InitialQty,
    int ReplenishmentThreshold,
    int TargetStockingLevel) : IRequest<Result<StockLevelDto>>;
