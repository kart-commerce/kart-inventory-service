using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.ReplenishStock;

/// <summary>
/// POST /inventory/replenish (contracts/api-contract.yaml's Implementation Addendum) - INV-8,
/// threshold-triggered automated reorder or manual admin path, both through this identical write
/// path (requirement-spec.md Decision 5).
/// </summary>
public sealed record ReplenishStockCommand(string WarehouseId, string Sku, int QtyAdded) : IRequest<Result<StockLevelDto>>;
