using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>Persistence abstraction for the Reservation aggregate (one repository per aggregate root).</summary>
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (SELECT ... FOR UPDATE) the reservation row and eagerly loads its Allocations -
    /// every release trigger (explicit call, OrderCancelled, OrderCompensationTriggered, TTL
    /// sweep) runs through this same locked read (ReservationReleaseService).
    /// </summary>
    Task<Reservation?> GetForUpdateAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>
    /// database-design.md idx_reservations_order_id (partial, status IN ('reserved','committed'))
    /// - the OrderCancelled/OrderCompensationTriggered/OrderConfirmed consumers' "find the live
    /// reservation(s) for this orderId" lookup. "Live" means Reserved OR Committed - a Committed
    /// (paid) reservation must still be releasable on a later cancellation/compensation, and is a
    /// safe idempotent no-op if handed to Commit again. Plain (non-locking) read - each returned
    /// reservation is locked individually inside ReservationReleaseService/ReservationCommitService.
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetReservedByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// database-design.md idx_reservations_expiry_sweep (partial, status = 'reserved') - the TTL
    /// sweep's "find every still-reserved hold whose expiry has passed" scan, bounded to
    /// batchSize per tick. Plain (non-locking) read - each id is locked individually inside
    /// ReservationReleaseService.ReleaseAsync.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetExpiredReservationIdsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Order Management (Admin) flow's "Assign Warehouse" view — every reservation (any status,
    /// unlike <see cref="GetReservedByOrderIdAsync"/>'s live-only filter) for this order, with
    /// Allocations eagerly loaded, so an admin can see which warehouse(s) actually fulfilled each
    /// line item even after the reservation has since been released/expired. Plain (non-locking)
    /// read — this is a read-only admin view, never a write path.
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
