using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;

namespace KartInventoryService.IntegrationTests.Fakes;

/// <summary>No real Redis in these Testcontainers-backed tests - a no-op cache-aside, same shape as ContractTests' NullStockCache.</summary>
public sealed class NullStockCache : IStockCache
{
    public Task<StockLevelDto?> GetAsync(string sku, string? warehouseId, CancellationToken cancellationToken) =>
        Task.FromResult<StockLevelDto?>(null);

    public Task SetAsync(string sku, string? warehouseId, StockLevelDto value, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string sku, string warehouseId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
