using KartInventoryService.Application.Common.Interfaces;

namespace KartInventoryService.ContractTests.Fakes;

/// <summary>
/// The in-memory repositories persist changes via direct object mutation, so there is nothing
/// for SaveChanges/transactions to actually do in this contract-test host.
/// </summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SetLockTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
}
