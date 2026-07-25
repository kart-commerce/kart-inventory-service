using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Features.ReplenishStock;

public sealed class ReplenishStockCommandHandler : IRequestHandler<ReplenishStockCommand, Result<StockLevelDto>>
{
    private readonly IWarehouseStockRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReplenishStockCommandHandler> _logger;

    public ReplenishStockCommandHandler(
        IWarehouseStockRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<ReplenishStockCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<StockLevelDto>> Handle(ReplenishStockCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Same lock-compatible write path as reservation/release (edge-cases.md, "Replenishment
        // racing an active reservation") - no separate concurrency mechanism for this write.
        var stock = await _repository.GetForUpdateAsync(request.WarehouseId, request.Sku, cancellationToken);
        if (stock is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockLevelDto>(Error.NotFound(
                $"warehouse_stock for ({request.WarehouseId}, {request.Sku}) has never been provisioned."));
        }

        var replenishResult = stock.Replenish(request.QtyAdded, actingPrincipal, now);
        if (replenishResult.IsFailure)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockLevelDto>(replenishResult.Error);
        }

        if (stock.AvailableQty < stock.ReplenishmentThreshold)
        {
            _logger.LogWarning(
                "Sku {Sku} in warehouse {WarehouseId} is still below its replenishment threshold ({AvailableQty} < {Threshold}) after replenishment.",
                stock.Sku,
                stock.WarehouseId,
                stock.AvailableQty,
                stock.ReplenishmentThreshold);
        }

        // Writes the warehouse_stock credit and the InventoryReplenished outbox row (via
        // DomainEvents-scanning SaveChanges override) in one commit.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result.Success(new StockLevelDto(stock.Sku, stock.WarehouseId, stock.AvailableQty));
    }
}
