using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using KartInventoryService.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Common.Services;

/// <summary>
/// The one release implementation every trigger shares (tickets.md's Sprint Planner note: INV-2/
/// INV-3/INV-4/INV-5 "share nearly all their domain logic and differ only in trigger source" -
/// build the shared logic once). Locks the reservation row, calls Reservation.Release, and only
/// credits WarehouseStock back / commits a real state change when a genuine transition happened
/// (ReleaseOutcome.Released) - never on the idempotent no-op against an already-terminal
/// reservation, which is exactly what makes release safe under at-least-once delivery and racing
/// triggers (design-decisions.md, "Release Idempotency & State Machine Design").
/// </summary>
public sealed class ReservationReleaseService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IWarehouseStockRepository _stockRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockCache _stockCache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationReleaseService> _logger;

    public ReservationReleaseService(
        IReservationRepository reservationRepository,
        IWarehouseStockRepository stockRepository,
        IUnitOfWork unitOfWork,
        IStockCache stockCache,
        TimeProvider timeProvider,
        ILogger<ReservationReleaseService> logger)
    {
        _reservationRepository = reservationRepository;
        _stockRepository = stockRepository;
        _unitOfWork = unitOfWork;
        _stockCache = stockCache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<ReservationDto>> ReleaseAsync(
        Guid reservationId,
        ReservationReleaseReason reason,
        string actingPrincipal,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        _logger.LogInformation(
            "Stage {Stage}: releasing reservation {ReservationId}, reason {Reason}.",
            "ReservationReleaseStarted",
            reservationId,
            reason);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var reservation = await _reservationRepository.GetForUpdateAsync(reservationId, cancellationToken);
        if (reservation is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<ReservationDto>(Error.NotFound($"Reservation '{reservationId}' not found."));
        }

        var releaseResult = reservation.Release(reason, actingPrincipal, now);

        if (releaseResult.Value == ReleaseOutcome.Released)
        {
            foreach (var allocation in reservation.Allocations)
            {
                var stock = await _stockRepository.GetForUpdateAsync(allocation.WarehouseId, reservation.Sku, cancellationToken);
                if (stock is null)
                {
                    // Defensive only - warehouse_stock rows are never deleted (tickets.md's
                    // flagged gap: no warehouse master-data lifecycle exists in this domain).
                    _logger.LogError(
                        "Cannot credit back release for reservation {ReservationId}: warehouse_stock ({WarehouseId}, {Sku}) no longer exists.",
                        reservationId,
                        allocation.WarehouseId,
                        reservation.Sku);
                    continue;
                }

                stock.Credit(allocation.Qty, actingPrincipal, now);
            }

            _logger.LogInformation(
                "Stage {Stage}: reservation {ReservationId} released, {AllocationCount} warehouse allocation(s) credited back.",
                "WarehouseStockCredited",
                reservationId,
                reservation.Allocations.Count);
        }
        else
        {
            _logger.LogInformation(
                "Stage {Stage}: reservation {ReservationId} was already terminal - no-op.",
                "ReservationReleaseNoOp",
                reservationId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        if (releaseResult.Value == ReleaseOutcome.Released)
        {
            _logger.LogInformation(
                "Stage {Stage}: reservation {ReservationId} release committed (outbox row saved).",
                "InventoryReleasedOutboxEventSaved",
                reservationId);

            // "Stock Sync Across Channels" - see ReserveStockCommandHandler's identical call.
            foreach (var allocation in reservation.Allocations)
            {
                await _stockCache.InvalidateAsync(reservation.Sku, allocation.WarehouseId, cancellationToken);
            }
        }

        return Result.Success(ReservationDto.FromDomain(reservation));
    }
}
