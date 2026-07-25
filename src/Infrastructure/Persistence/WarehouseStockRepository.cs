using KartInventoryService.Application.Common.Exceptions;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KartInventoryService.Infrastructure.Persistence;

public sealed class WarehouseStockRepository : IWarehouseStockRepository
{
    private const string LockNotAvailableSqlState = "55P03";

    private readonly InventoryDbContext _dbContext;

    public WarehouseStockRepository(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseStock?> GetAsync(string warehouseId, string sku, CancellationToken cancellationToken) =>
        await _dbContext.WarehouseStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.Sku == sku, cancellationToken);

    public async Task<IReadOnlyList<WarehouseStock>> GetAllForSkuAsync(string sku, CancellationToken cancellationToken) =>
        await _dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(s => s.Sku == sku)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseStock>> GetForUpdateBySkuAscendingAsync(string sku, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.WarehouseStocks
                .FromSqlInterpolated($"SELECT * FROM warehouse_stock WHERE sku = {sku} ORDER BY warehouse_id ASC FOR UPDATE")
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == LockNotAvailableSqlState)
        {
            throw new LockAcquisitionTimeoutException($"Timed out waiting for a stock lock on sku '{sku}'.", ex);
        }
    }

    public async Task<WarehouseStock?> GetForUpdateAsync(string warehouseId, string sku, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.WarehouseStocks
                .FromSqlInterpolated($"SELECT * FROM warehouse_stock WHERE warehouse_id = {warehouseId} AND sku = {sku} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == LockNotAvailableSqlState)
        {
            throw new LockAcquisitionTimeoutException($"Timed out waiting for a stock lock on ({warehouseId}, {sku}).", ex);
        }
    }
}
