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

    /// <summary>
    /// Inventory &amp; Stock Management flow's "Stock Sync Across Channels" stage: every write path
    /// (reserve/release/replenish/reconcile) shares this exact cache-aside store with BOTH read
    /// channels - GET /inventory/{sku} (HTTP) and the gRPC CheckAvailability RPC consumed by
    /// kart-cart-service - but before this flow, no write ever invalidated it, so both channels
    /// could serve a stale value for up to StockCacheTtlSeconds after a write. Removes both the
    /// per-warehouse entry and the platform-wide summed-across-warehouses entry for this sku, so
    /// the next read on either channel always reflects the just-committed state instead of racing
    /// a short-TTL stale cache.
    /// </summary>
    Task InvalidateAsync(string sku, string warehouseId, CancellationToken cancellationToken);
}
