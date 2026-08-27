using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetLowStock;

public sealed class GetLowStockQueryHandler : IRequestHandler<GetLowStockQuery, Result<IReadOnlyList<StockLevelDto>>>
{
    private readonly IWarehouseStockRepository _repository;

    public GetLowStockQueryHandler(IWarehouseStockRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<StockLevelDto>>> Handle(GetLowStockQuery request, CancellationToken cancellationToken)
    {
        var rows = await _repository.GetLowStockAsync(request.WarehouseId, cancellationToken);
        IReadOnlyList<StockLevelDto> dtos = rows.Select(r => new StockLevelDto(r.Sku, r.WarehouseId, r.AvailableQty)).ToList();
        return Result.Success(dtos);
    }
}
