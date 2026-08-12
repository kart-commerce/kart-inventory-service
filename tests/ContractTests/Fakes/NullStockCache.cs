using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;

namespace KartInventoryService.ContractTests.Fakes;

public sealed class NullStockCache : IStockCache
{
    public Task<StockLevelDto?> GetAsync(string sku, string? warehouseId, CancellationToken cancellationToken) =>
        Task.FromResult<StockLevelDto?>(null);

    public Task SetAsync(string sku, string? warehouseId, StockLevelDto value, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string sku, string warehouseId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
