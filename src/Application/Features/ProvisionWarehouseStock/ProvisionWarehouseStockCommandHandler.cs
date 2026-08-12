using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using KartInventoryService.Domain.Inventory;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Features.ProvisionWarehouseStock;

public sealed class ProvisionWarehouseStockCommandHandler : IRequestHandler<ProvisionWarehouseStockCommand, Result<StockLevelDto>>
{
    private readonly IWarehouseStockRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProvisionWarehouseStockCommandHandler> _logger;

    public ProvisionWarehouseStockCommandHandler(
        IWarehouseStockRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<ProvisionWarehouseStockCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<StockLevelDto>> Handle(ProvisionWarehouseStockCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        _logger.LogInformation(
            "Stage {Stage}: provisioning warehouse {WarehouseId}, sku {Sku}, initial qty {InitialQty}.",
            "WarehouseStockProvisionRequested",
            request.WarehouseId,
            request.Sku,
            request.InitialQty);

        var existing = await _repository.GetAsync(request.WarehouseId, request.Sku, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<StockLevelDto>(Error.Validation(
                $"warehouse_stock for ({request.WarehouseId}, {request.Sku}) has already been provisioned."));
        }

        var provisionResult = WarehouseStock.Provision(
            request.WarehouseId,
            request.Sku,
            request.InitialQty,
            request.ReplenishmentThreshold,
            request.TargetStockingLevel,
            actingPrincipal,
            now);

        if (provisionResult.IsFailure)
        {
            return Result.Failure<StockLevelDto>(provisionResult.Error);
        }

        var stock = provisionResult.Value;
        await _repository.AddAsync(stock, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: warehouse {WarehouseId}, sku {Sku} provisioned (outbox row saved).",
            "WarehouseStockProvisioned",
            stock.WarehouseId,
            stock.Sku);

        return Result.Success(new StockLevelDto(stock.Sku, stock.WarehouseId, stock.AvailableQty));
    }
}
