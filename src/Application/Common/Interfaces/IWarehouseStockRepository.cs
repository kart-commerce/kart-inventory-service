using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction for the WarehouseStock aggregate (coding-standards.md DIP: Application
/// owns this interface, Infrastructure implements it). One repository per aggregate root - never
/// a generic IRepository&lt;T&gt;.
/// </summary>
public interface IWarehouseStockRepository
{
    /// <summary>Plain (non-locking) single-warehouse read - GET /inventory/{sku}?warehouseId=... cache-miss fallback.</summary>
    Task<WarehouseStock?> GetAsync(string warehouseId, string sku, CancellationToken cancellationToken);

    /// <summary>Plain (non-locking) read of every warehouse row for a SKU - GET /inventory/{sku} with no warehouseId, summed across warehouses.</summary>
    Task<IReadOnlyList<WarehouseStock>> GetAllForSkuAsync(string sku, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (SELECT ... FOR UPDATE) every warehouse_stock row for this SKU, ordered ascending by
    /// warehouse_id (requirement-spec.md Decision 3's deadlock-avoidance ordering for the
    /// multi-warehouse fallback) - must run inside a transaction opened via
    /// IUnitOfWork.BeginTransactionAsync. Throws LockAcquisitionTimeoutException if the bounded
    /// lock_timeout (IUnitOfWork.SetLockTimeoutAsync) is exceeded.
    /// </summary>
    Task<IReadOnlyList<WarehouseStock>> GetForUpdateBySkuAscendingAsync(string sku, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (SELECT ... FOR UPDATE) a single warehouse_stock row - used by replenishment
    /// (INV-8) and by the release credit-back path (ReservationReleaseService), both of which
    /// touch exactly one row per warehouse rather than every candidate row for a SKU.
    /// </summary>
    Task<WarehouseStock?> GetForUpdateAsync(string warehouseId, string sku, CancellationToken cancellationToken);
}
