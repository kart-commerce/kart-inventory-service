using System.Text.Json;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Common.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KartInventoryService.Infrastructure.Caching;

/// <summary>
/// database-design.md's `inventory:stock:{sku}[:{warehouseId}]` short-TTL cache-aside
/// (design-decisions.md, "Read-Path Caching Strategy"). A miss returns null so the caller falls
/// back to IWarehouseStockRepository - Redis availability is a latency, not a correctness,
/// dependency, exactly like kart-category-service's RedisCategoryCache.
/// </summary>
public sealed class RedisStockCache : IStockCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly TimeSpan _ttl;

    public RedisStockCache(IConnectionMultiplexer connectionMultiplexer, IOptions<InventoryOptions> options)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _ttl = TimeSpan.FromSeconds(options.Value.StockCacheTtlSeconds);
    }

    public async Task<StockLevelDto?> GetAsync(string sku, string? warehouseId, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(Key(sku, warehouseId));
        return value.HasValue
            ? JsonSerializer.Deserialize<StockLevelDto>(value!, SerializerOptions)
            : null;
    }

    public async Task SetAsync(string sku, string? warehouseId, StockLevelDto value, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await database.StringSetAsync(Key(sku, warehouseId), json, _ttl);
    }

    public async Task InvalidateAsync(string sku, string warehouseId, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(Key(sku, warehouseId));
        await database.KeyDeleteAsync(Key(sku, warehouseId: null));
    }

    private static string Key(string sku, string? warehouseId) =>
        warehouseId is null ? $"inventory:stock:{sku}" : $"inventory:stock:{sku}:{warehouseId}";
}
