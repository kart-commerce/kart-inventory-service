using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetStockLevel;

/// <summary>
/// GET /inventory/{sku} (api-contract.yaml getStockLevel) and the gRPC CheckAvailability RPC both
/// reuse this same query/cache-aside read path (INV-7 depends on INV-6 - tickets.md). WarehouseId
/// null sums available quantity across every warehouse for the SKU.
/// </summary>
public sealed record GetStockLevelQuery(string Sku, string? WarehouseId) : IRequest<Result<StockLevelDto>>;
