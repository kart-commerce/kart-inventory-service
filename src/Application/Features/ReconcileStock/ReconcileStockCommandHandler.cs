using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Features.ReconcileStock;

public sealed class ReconcileStockCommandHandler : IRequestHandler<ReconcileStockCommand, Result<StockReconciliationResultDto>>
{
    private readonly IWarehouseStockRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockCache _stockCache;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReconcileStockCommandHandler> _logger;

    public ReconcileStockCommandHandler(
        IWarehouseStockRepository repository,
        IUnitOfWork unitOfWork,
        IStockCache stockCache,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<ReconcileStockCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _stockCache = stockCache;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<StockReconciliationResultDto>> Handle(ReconcileStockCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        _logger.LogInformation(
            "Stage {Stage}: reconciliation requested for warehouse {WarehouseId}, sku {Sku}, counted qty {CountedQty}, reason {Reason}.",
            "StockReconciliationRequested",
            request.WarehouseId,
            request.Sku,
            request.CountedQty,
            request.Reason);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var stock = await _repository.GetForUpdateAsync(request.WarehouseId, request.Sku, cancellationToken);
        if (stock is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockReconciliationResultDto>(Error.NotFound(
                $"warehouse_stock for ({request.WarehouseId}, {request.Sku}) has never been provisioned."));
        }

        var previousQty = stock.AvailableQty;
        var reconcileResult = stock.Reconcile(request.CountedQty, request.Reason, actingPrincipal, now);
        if (reconcileResult.IsFailure)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockReconciliationResultDto>(reconcileResult.Error);
        }

        _logger.LogInformation(
            "Stage {Stage}: warehouse {WarehouseId}, sku {Sku} variance {Variance} ({PreviousQty} -> {CountedQty}).",
            "StockVarianceComputed",
            stock.WarehouseId,
            stock.Sku,
            reconcileResult.Value,
            previousQty,
            request.CountedQty);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: warehouse {WarehouseId}, sku {Sku} reconciled (outbox row saved).",
            "StockReconciledOutboxEventSaved",
            stock.WarehouseId,
            stock.Sku);

        await _stockCache.InvalidateAsync(stock.Sku, stock.WarehouseId, cancellationToken);

        return Result.Success(new StockReconciliationResultDto(stock.Sku, stock.WarehouseId, previousQty, stock.AvailableQty, reconcileResult.Value));
    }
}
