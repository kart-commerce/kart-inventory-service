namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Terminal-state machine (design-decisions.md, "Release Idempotency & State Machine Design"):
/// once Released or Expired, no further transition is possible - any release trigger against a
/// terminal reservation is a no-op that still returns success.
/// </summary>
public enum ReservationStatus
{
    Reserved,
    Released,
    Expired
}
