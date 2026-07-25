using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetStockLevel;

public sealed class GetStockLevelQueryHandler : IRequestHandler<GetStockLevelQuery, Result<StockLevelDto>>
{
    private readonly IStockCache _cache;
    private readonly IWarehouseStockRepository _repository;

    public GetStockLevelQueryHandler(IStockCache cache, IWarehouseStockRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Result<StockLevelDto>> Handle(GetStockLevelQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(request.Sku, request.WarehouseId, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        StockLevelDto dto;
        if (request.WarehouseId is not null)
        {
            var stock = await _repository.GetAsync(request.WarehouseId, request.Sku, cancellationToken);
            if (stock is null)
            {
                return Result.Failure<StockLevelDto>(Error.NotFound(
                    $"No stock found for sku '{request.Sku}' in warehouse '{request.WarehouseId}'."));
            }

            dto = new StockLevelDto(request.Sku, request.WarehouseId, stock.AvailableQty);
        }
        else
        {
            var rows = await _repository.GetAllForSkuAsync(request.Sku, cancellationToken);
            if (rows.Count == 0)
            {
                return Result.Failure<StockLevelDto>(Error.NotFound($"No stock found for sku '{request.Sku}'."));
            }

            dto = new StockLevelDto(request.Sku, null, rows.Sum(r => r.AvailableQty));
        }

        await _cache.SetAsync(request.Sku, request.WarehouseId, dto, cancellationToken);
        return Result.Success(dto);
    }
}
