using KartInventoryService.Application.Common.Exceptions;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Common.Options;
using KartInventoryService.Domain.Common;
using KartInventoryService.Domain.Inventory;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KartInventoryService.Application.Features.ReserveStock;

public sealed class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, Result<ReservationDto>>
{
    private readonly IWarehouseStockRepository _stockRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxEventWriter _outboxEventWriter;
    private readonly IStockCache _stockCache;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly InventoryOptions _options;
    private readonly ILogger<ReserveStockCommandHandler> _logger;

    public ReserveStockCommandHandler(
        IWarehouseStockRepository stockRepository,
        IReservationRepository reservationRepository,
        IUnitOfWork unitOfWork,
        IOutboxEventWriter outboxEventWriter,
        IStockCache stockCache,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        IOptions<InventoryOptions> options,
        ILogger<ReserveStockCommandHandler> logger)
    {
        _stockRepository = stockRepository;
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
        _outboxEventWriter = outboxEventWriter;
        _stockCache = stockCache;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<ReservationDto>> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        _logger.LogInformation(
            "Stage {Stage}: reserve requested for order {OrderId}, sku {Sku}, qty {Qty}.",
            "ReserveStockHandlerStarted",
            request.OrderId,
            request.Sku,
            request.Qty);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _unitOfWork.SetLockTimeoutAsync(TimeSpan.FromMilliseconds(_options.LockTimeoutMs), cancellationToken);

        IReadOnlyList<WarehouseStock> candidates;
        try
        {
            // Single query, ascending warehouse_id: satisfies the multi-warehouse fallback's
            // deadlock-avoidance lock ordering (requirement-spec.md Decision 3) while letting
            // allocation-selection below prefer a single row that alone covers qty (the
            // "single-warehouse-first" outcome) without a second lock round-trip.
            candidates = await _stockRepository.GetForUpdateBySkuAscendingAsync(request.Sku, cancellationToken);
        }
        catch (LockAcquisitionTimeoutException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogWarning(
                "Stage {Stage}: reserve rejected, timed out waiting for a stock lock on sku {Sku} (order {OrderId}).",
                "ReserveStockLockTimeout",
                request.Sku,
                request.OrderId);
            return Result.Failure<ReservationDto>(Error.LockTimeout(
                $"Timed out waiting for a stock lock on '{request.Sku}' - retry."));
        }

        if (candidates.Count == 0)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogWarning(
                "Stage {Stage}: reserve rejected, no warehouse stock provisioned for sku {Sku} (order {OrderId}).",
                "ReserveStockSkuNotProvisioned",
                request.Sku,
                request.OrderId);
            return Result.Failure<ReservationDto>(Error.NotFound($"No warehouse stock is provisioned for sku '{request.Sku}'."));
        }

        var totalAvailable = candidates.Sum(c => c.AvailableQty);
        if (totalAvailable < request.Qty)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);

            // requirement-spec.md Decision 4: hard fail, no backorder path. InventoryReservationFailed
            // is not attached to any aggregate (no Reservation is ever created here), so it's
            // written directly via IOutboxEventWriter rather than through the DomainEvents/
            // SaveChanges pattern the other three published events use.
            await _outboxEventWriter.EnqueueAsync(
                "InventoryReservationFailed",
                aggregateRef: $"{request.OrderId}:{request.Sku}",
                payload: new { orderId = request.OrderId, sku = request.Sku },
                actingPrincipal,
                cancellationToken);

            _logger.LogWarning(
                "Stage {Stage}: order {OrderId}, sku {Sku} - {Available} available, {Requested} requested.",
                "InventoryReserveFailed",
                request.OrderId,
                request.Sku,
                totalAvailable,
                request.Qty);

            return Result.Failure<ReservationDto>(Error.InsufficientStock(
                $"Insufficient stock for '{request.Sku}': {totalAvailable} available, {request.Qty} requested."));
        }

        var allocations = ChooseAllocations(candidates, request.Qty);
        _logger.LogInformation(
            "Stage {Stage}: order {OrderId}, sku {Sku} allocated across {WarehouseCount} warehouse(s).",
            "WarehouseAllocationChosen",
            request.OrderId,
            request.Sku,
            allocations.Count);

        foreach (var (warehouseId, allocatedQty) in allocations)
        {
            var stock = candidates.Single(c => c.WarehouseId == warehouseId);
            var debitResult = stock.TryDebit(allocatedQty, actingPrincipal, now);
            if (debitResult.IsFailure)
            {
                // Cannot happen given the totalAvailable check above under the row lock we hold -
                // kept as a defensive Result-based guard rather than an assumption.
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogWarning(
                    "Stage {Stage}: reserve rejected, debit failed for warehouse {WarehouseId}, sku {Sku} (order {OrderId}): {Reason}.",
                    "ReserveStockDebitFailed",
                    warehouseId,
                    request.Sku,
                    request.OrderId,
                    debitResult.Error.Message);
                return Result.Failure<ReservationDto>(debitResult.Error);
            }

            if (stock.AvailableQty < stock.ReplenishmentThreshold)
            {
                // requirement-spec.md Decision 5: threshold-based reorder signal - WarehouseStock
                // itself already raised LowStockDetected (a real published event, Domain has zero
                // framework deps so can't log); this is the Application-layer Stage log for the
                // same crossing, visible in Grafana alongside the rest of this request's story.
                _logger.LogInformation(
                    "Stage {Stage}: sku {Sku} in warehouse {WarehouseId} is below its replenishment threshold ({AvailableQty} < {Threshold}).",
                    "LowStockThresholdBreached",
                    stock.Sku,
                    stock.WarehouseId,
                    stock.AvailableQty,
                    stock.ReplenishmentThreshold);
            }
        }

        _logger.LogInformation(
            "Stage {Stage}: order {OrderId}, sku {Sku} debited across every allocation.",
            "WarehouseStockDebited",
            request.OrderId,
            request.Sku);

        var reservationResult = Reservation.Create(
            request.OrderId,
            request.Sku,
            request.Qty,
            allocations,
            TimeSpan.FromMinutes(_options.ReservationTtlMinutes),
            actingPrincipal,
            now);

        if (reservationResult.IsFailure)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogWarning(
                "Stage {Stage}: reserve rejected, reservation creation failed for order {OrderId}, sku {Sku}: {Reason}.",
                "ReserveStockReservationCreationFailed",
                request.OrderId,
                request.Sku,
                reservationResult.Error.Message);
            return Result.Failure<ReservationDto>(reservationResult.Error);
        }

        var reservation = reservationResult.Value;
        await _reservationRepository.AddAsync(reservation, cancellationToken);
        _logger.LogInformation(
            "Stage {Stage}: reservation {ReservationId} created for order {OrderId}.",
            "ReservationPersisted",
            reservation.ReservationId,
            request.OrderId);

        // Writes the reservations/reservation_allocations rows, the warehouse_stock debits, and
        // the InventoryReserved outbox row (via DomainEvents-scanning SaveChanges override) all
        // in this one commit - the Outbox pattern's atomicity guarantee (design-decisions.md).
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);
        _logger.LogInformation(
            "Stage {Stage}: reservation {ReservationId} for order {OrderId} committed (outbox row saved).",
            "InventoryReservedOutboxEventSaved",
            reservation.ReservationId,
            request.OrderId);

        // "Stock Sync Across Channels": both GET /inventory/{sku} and the gRPC availability RPC
        // read through this same cache-aside store - invalidate every warehouse this reservation
        // touched so neither channel serves a stale pre-debit value.
        foreach (var (warehouseId, _) in allocations)
        {
            await _stockCache.InvalidateAsync(request.Sku, warehouseId, cancellationToken);
        }

        return Result.Success(ReservationDto.FromDomain(reservation));
    }

    /// <summary>
    /// Prefers a single warehouse that alone covers qty (the "single-warehouse-first" outcome);
    /// falls back to a greedy multi-warehouse split across the already-locked, ascending-
    /// warehouse_id-ordered candidates only when no single one suffices (requirement-spec.md
    /// Decision 3). Callers have already verified totalAvailable >= qty.
    /// </summary>
    private static List<(string WarehouseId, int Qty)> ChooseAllocations(IReadOnlyList<WarehouseStock> candidatesAscending, int qty)
    {
        var singleWarehouse = candidatesAscending.FirstOrDefault(c => c.AvailableQty >= qty);
        if (singleWarehouse is not null)
        {
            return new List<(string, int)> { (singleWarehouse.WarehouseId, qty) };
        }

        var allocations = new List<(string WarehouseId, int Qty)>();
        var remaining = qty;
        foreach (var candidate in candidatesAscending)
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(candidate.AvailableQty, remaining);
            if (take <= 0)
            {
                continue;
            }

            allocations.Add((candidate.WarehouseId, take));
            remaining -= take;
        }

        return allocations;
    }
}
