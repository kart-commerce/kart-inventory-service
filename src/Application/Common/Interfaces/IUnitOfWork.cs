namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>
/// Commits the PostgreSQL transaction for the current request. Infrastructure's implementation
/// also converts any pending domain events raised on tracked aggregates into
/// inventory_outbox_events rows within this same call (mirrors kart-category-service's
/// EfUnitOfWork/CategoryDbContext pattern) - Application code never writes outbox rows itself for
/// aggregate-attached events (see IOutboxEventWriter for the one event that isn't).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens the ambient transaction every locking write (ReserveStock, ReservationReleaseService,
    /// ReplenishStock) must run inside - a lock taken outside an explicit transaction is released
    /// the instant its own statement completes.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitTransactionAsync(CancellationToken cancellationToken);

    Task RollbackTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// design-decisions.md "Resilience Budget": sets a bounded lock_timeout for the remainder of
    /// the current transaction, so a SELECT ... FOR UPDATE against a hot SKU fails fast
    /// (LockAcquisitionTimeoutException) instead of queuing indefinitely. Must be called after
    /// BeginTransactionAsync (lock_timeout set outside a transaction has no effect beyond the
    /// current statement).
    /// </summary>
    Task SetLockTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
