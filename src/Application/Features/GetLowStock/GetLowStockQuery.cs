using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetLowStock;

/// <summary>Inventory &amp; Stock Management flow's "Reorder Alert" dashboard view - every SKU/warehouse currently below its own ReplenishmentThreshold.</summary>
public sealed record GetLowStockQuery(string? WarehouseId) : IRequest<Result<IReadOnlyList<StockLevelDto>>>;
