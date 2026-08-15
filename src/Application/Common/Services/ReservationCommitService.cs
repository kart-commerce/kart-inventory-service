using KartInventoryService.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Common.Services;

/// <summary>
/// Inventory &amp; Stock Management flow's "Deduct (Order Confirmed)" stage - the counterpart to
/// ReservationReleaseService, but for the Commit transition. Consuming OrderConfirmed looks up
/// every live (Reserved or Committed) reservation for the order and commits each one; committing
/// an already-Committed reservation is a safe no-op (Reservation.Commit's own idempotency), which
/// is what makes this safe under at-least-once RabbitMQ delivery.
/// </summary>
public sealed class ReservationCommitService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationCommitService> _logger;

    public ReservationCommitService(
        IReservationRepository reservationRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ReservationCommitService> logger)
    {
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task CommitOrderAsync(Guid orderId, string actingPrincipal, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var live = await _reservationRepository.GetReservedByOrderIdAsync(orderId, cancellationToken);

        foreach (var reservation in live)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var locked = await _reservationRepository.GetForUpdateAsync(reservation.ReservationId, cancellationToken);
            if (locked is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogWarning(
                    "Stage {Stage}: order {OrderId} reservation {ReservationId} vanished before its commit lock could be acquired; skipping.",
                    "ReservationCommitNoOp",
                    orderId,
                    reservation.ReservationId);
                continue;
            }

            locked.Commit(actingPrincipal, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Stage {Stage}: order {OrderId} committed {Count} live reservation(s).",
            "ReservationCommitted",
            orderId,
            live.Count);
    }
}
