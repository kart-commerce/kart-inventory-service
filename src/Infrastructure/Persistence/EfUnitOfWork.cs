using KartInventoryService.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KartInventoryService.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task SetLockTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        // PostgreSQL's SET LOCAL does not accept a bind parameter (SET requires a literal), so
        // this can't use ExecuteSqlInterpolated/ExecuteSqlAsync - safe here because milliseconds
        // is a validated int from InventoryOptions, never user input.
        var milliseconds = (int)timeout.TotalMilliseconds;
        var sql = $"SET LOCAL lock_timeout = '{milliseconds}ms'";
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
    }
}
