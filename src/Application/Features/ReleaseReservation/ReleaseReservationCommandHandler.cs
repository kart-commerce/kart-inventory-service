using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Common.Services;
using KartInventoryService.Domain.Common;
using KartInventoryService.Domain.Inventory;
using MediatR;

namespace KartInventoryService.Application.Features.ReleaseReservation;

public sealed class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, Result<ReservationDto>>
{
    private readonly ReservationReleaseService _releaseService;
    private readonly ICurrentPrincipal _currentPrincipal;

    public ReleaseReservationCommandHandler(ReservationReleaseService releaseService, ICurrentPrincipal currentPrincipal)
    {
        _releaseService = releaseService;
        _currentPrincipal = currentPrincipal;
    }

    public Task<Result<ReservationDto>> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken) =>
        _releaseService.ReleaseAsync(
            request.ReservationId,
            ReservationReleaseReason.ExplicitCall,
            _currentPrincipal.ActingPrincipal,
            cancellationToken);
}
