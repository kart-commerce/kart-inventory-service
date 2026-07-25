using KartInventoryService.Application.Common.Models;

namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>
/// Short-TTL Redis cache-aside in front of PostgreSQL for GET /inventory/{sku} and the gRPC
/// CheckAvailability RPC (design-decisions.md, "Read-Path Caching Strategy"). The write path
/// (reserve/release/replenish) never reads through this - it always takes a fresh
/// SELECT ... FOR UPDATE, so the oversell invariant is never exposed to cached staleness. A miss
/// here always falls back to IWarehouseStockRepository; Redis availability is a latency, not a
/// correctness, dependency.
/// </summary>
public interface IStockCache
{
    /// <summary>inventory:stock:{sku}[:{warehouseId}] - warehouseId null means the platform-wide summed-across-warehouses entry.</summary>
    Task<StockLevelDto?> GetAsync(string sku, string? warehouseId, CancellationToken cancellationToken);

    Task SetAsync(string sku, string? warehouseId, StockLevelDto value, CancellationToken cancellationToken);
}
