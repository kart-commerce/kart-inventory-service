namespace KartInventoryService.Domain.Common;

/// <summary>
/// Domain/business error, returned via Result rather than thrown (api-standards.md: "Domain/
/// business errors use a Result/Either pattern - not exceptions"). Code values match
/// contracts/api-contract.yaml's Problem.code and Api/Common/ResultExtensions.cs's status-code
/// mapping (e.g. "insufficient_stock" -> 409, "lock_timeout" -> 503).
/// </summary>
public sealed class Error
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public string Code { get; }
    public string Message { get; }

    private Error(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public static Error Validation(string message) => new("validation_error", message);

    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>requirement-spec.md Decision 4: insufficient stock always yields a hard failure - no backorder path.</summary>
    public static Error InsufficientStock(string message) => new("insufficient_stock", message);

    /// <summary>design-decisions.md "Resilience Budget": a bounded lock_timeout was exceeded acquiring SELECT ... FOR UPDATE - retryable, distinct from a genuine oversell failure.</summary>
    public static Error LockTimeout(string message) => new("lock_timeout", message);
}
