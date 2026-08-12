using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetOrderAllocations;

public sealed class GetOrderAllocationsQueryHandler : IRequestHandler<GetOrderAllocationsQuery, Result<IReadOnlyList<ReservationDto>>>
{
    private readonly IReservationRepository _repository;

    public GetOrderAllocationsQueryHandler(IReservationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<ReservationDto>>> Handle(GetOrderAllocationsQuery request, CancellationToken cancellationToken)
    {
        var reservations = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);

        // Empty is a legitimate answer (no reservations were ever made for this order, e.g. it
        // failed reservation before any WarehouseAllocation existed) - not a 404. The caller can
        // distinguish "order doesn't exist" from "no allocations" itself, since this service has
        // no concept of Order existing/not existing (that's kart-order-service's aggregate).
        IReadOnlyList<ReservationDto> dtos = reservations.Select(ReservationDto.FromDomain).ToList();
        return Result.Success(dtos);
    }
}
