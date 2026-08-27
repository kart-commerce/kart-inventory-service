namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// State machine (design-decisions.md, "Release Idempotency & State Machine Design"): once
/// Released or Expired, no further transition is possible - any release trigger against a
/// terminal reservation is a no-op that still returns success. <see cref="Committed"/> (added for
/// Inventory &amp; Stock Management flow's "Deduct (Order Confirmed)" stage) is NOT terminal - a
/// paid order can still be admin-cancelled later, so a Committed reservation remains releasable
/// via ExplicitCall/OrderCancelled/CompensationTriggered. It exists solely so the TTL sweep
/// (idx_reservations_expiry_sweep, filtered to status = 'reserved') stops considering the
/// reservation once the order it backs has actually been paid - before this status existed, a
/// slow payment (> ReservationTtlMinutes) could have its stock auto-released by the sweep even
/// though the order had since been confirmed.
/// </summary>
public enum ReservationStatus
{
    Reserved,
    Committed,
    Released,
    Expired
}
