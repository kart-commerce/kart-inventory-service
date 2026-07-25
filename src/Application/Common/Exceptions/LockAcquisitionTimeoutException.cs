namespace KartInventoryService.Application.Common.Exceptions;

/// <summary>
/// Thrown by IWarehouseStockRepository's locking reads when the bounded lock_timeout (design-
/// decisions.md, "Resilience Budget") is exceeded acquiring SELECT ... FOR UPDATE on a hot SKU.
/// Kept provider-agnostic here (Infrastructure translates the underlying Npgsql/Postgres
/// exception into this) so Application never depends on Npgsql types directly. Handlers catch
/// this and map it to Error.LockTimeout (503) - distinct from a genuine Error.InsufficientStock
/// (409) oversell failure.
/// </summary>
public sealed class LockAcquisitionTimeoutException : Exception
{
    public LockAcquisitionTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
