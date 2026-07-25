using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Services;
using KartInventoryService.Domain.Inventory;
using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderCompensationTriggered;

public sealed class ConsumeOrderCompensationTriggeredCommandHandler : IRequestHandler<ConsumeOrderCompensationTriggeredCommand>
{
    private const string SystemPrincipal = "system:inventory-compensation-consumer";

    private readonly IReservationRepository _reservationRepository;
    private readonly ReservationReleaseService _releaseService;

    public ConsumeOrderCompensationTriggeredCommandHandler(IReservationRepository reservationRepository, ReservationReleaseService releaseService)
    {
        _reservationRepository = reservationRepository;
        _releaseService = releaseService;
    }

    public async Task Handle(ConsumeOrderCompensationTriggeredCommand request, CancellationToken cancellationToken)
    {
        var reserved = await _reservationRepository.GetReservedByOrderIdAsync(request.OrderId, cancellationToken);
        foreach (var reservation in reserved)
        {
            await _releaseService.ReleaseAsync(
                reservation.ReservationId,
                ReservationReleaseReason.CompensationTriggered,
                SystemPrincipal,
                cancellationToken);
        }
    }
}
