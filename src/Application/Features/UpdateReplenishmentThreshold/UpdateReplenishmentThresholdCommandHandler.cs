using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Features.UpdateReplenishmentThreshold;

public sealed class UpdateReplenishmentThresholdCommandHandler : IRequestHandler<UpdateReplenishmentThresholdCommand, Result<StockLevelDto>>
{
    private readonly IWarehouseStockRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateReplenishmentThresholdCommandHandler> _logger;

    public UpdateReplenishmentThresholdCommandHandler(
        IWarehouseStockRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<UpdateReplenishmentThresholdCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<StockLevelDto>> Handle(UpdateReplenishmentThresholdCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        _logger.LogInformation(
            "Stage {Stage}: threshold update requested for warehouse {WarehouseId}, sku {Sku} -> threshold {Threshold}, target {Target}.",
            "LowStockThresholdUpdateRequested",
            request.WarehouseId,
            request.Sku,
            request.ReplenishmentThreshold,
            request.TargetStockingLevel);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var stock = await _repository.GetForUpdateAsync(request.WarehouseId, request.Sku, cancellationToken);
        if (stock is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockLevelDto>(Error.NotFound(
                $"warehouse_stock for ({request.WarehouseId}, {request.Sku}) has never been provisioned."));
        }

        var updateResult = stock.UpdateThreshold(request.ReplenishmentThreshold, request.TargetStockingLevel, actingPrincipal, now);
        if (updateResult.IsFailure)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<StockLevelDto>(updateResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: warehouse {WarehouseId}, sku {Sku} threshold persisted.",
            "WarehouseStockThresholdPersisted",
            stock.WarehouseId,
            stock.Sku);

        return Result.Success(new StockLevelDto(stock.Sku, stock.WarehouseId, stock.AvailableQty));
    }
}
