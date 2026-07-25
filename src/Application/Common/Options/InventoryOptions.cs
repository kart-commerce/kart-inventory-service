namespace KartInventoryService.Application.Common.Options;

/// <summary>
/// Tunables the requirement-spec/design-decisions docs fix as engineering defaults - bound from
/// the "Inventory" configuration section (coding-standards.md: no magic numbers hardcoded in
/// handlers/hosted services).
/// </summary>
public sealed class InventoryOptions
{
    /// <summary>requirement-spec.md Decision 2: reservation hold TTL.</summary>
    public int ReservationTtlMinutes { get; set; } = 15;

    /// <summary>requirement-spec.md Decision 2: TTL sweep tick interval.</summary>
    public int SweepIntervalSeconds { get; set; } = 60;

    /// <summary>design-decisions.md "Resilience Budget": bounded lock_timeout on the reservation transaction's SELECT ... FOR UPDATE.</summary>
    public int LockTimeoutMs { get; set; } = 300;

    /// <summary>design-decisions.md "Read-Path Caching Strategy": short-TTL Redis cache-aside in front of GET /inventory/{sku}.</summary>
    public int StockCacheTtlSeconds { get; set; } = 5;

    /// <summary>Bounds how many expired reservations the sweep processes per tick.</summary>
    public int SweepBatchSize { get; set; } = 100;
}
