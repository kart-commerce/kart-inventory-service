namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// database-design.md `reservations.release_reason` - audit trail of which of the four converging
/// triggers actually caused a release; all four resolve to the identical idempotent write path
/// (Reservation.Release). Named ReservationReleaseReason (not ReleaseReason) to avoid colliding
/// with Reservation.ReleaseReason, the property that holds it.
/// </summary>
public enum ReservationReleaseReason
{
    ExplicitCall,
    OrderCancelled,
    CompensationTriggered,
    TtlExpiry
}
