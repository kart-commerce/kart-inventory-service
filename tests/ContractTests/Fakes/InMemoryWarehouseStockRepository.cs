using System.Collections.Concurrent;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.ContractTests.Fakes;

/// <summary>
/// In-memory fake for HTTP wire-shape contract tests only - no locking semantics (that's
/// IntegrationTests' job, against real PostgreSQL). Registered as a singleton so seeded data
/// survives across the single HTTP request each test issues.
/// </summary>
public sealed class InMemoryWarehouseStockRepository : IWarehouseStockRepository
{
    private readonly ConcurrentDictionary<(string WarehouseId, string Sku), WarehouseStock> _stocks = new();

    public void Seed(WarehouseStock stock) => _stocks[(stock.WarehouseId, stock.Sku)] = stock;

    public Task<WarehouseStock?> GetAsync(string warehouseId, string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_stocks.GetValueOrDefault((warehouseId, sku)));

    public Task<IReadOnlyList<WarehouseStock>> GetAllForSkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WarehouseStock>>(_stocks.Values.Where(s => s.Sku == sku).ToList());

    public Task<IReadOnlyList<WarehouseStock>> GetForUpdateBySkuAscendingAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WarehouseStock>>(_stocks.Values.Where(s => s.Sku == sku).OrderBy(s => s.WarehouseId).ToList());

    public Task<WarehouseStock?> GetForUpdateAsync(string warehouseId, string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_stocks.GetValueOrDefault((warehouseId, sku)));
}
