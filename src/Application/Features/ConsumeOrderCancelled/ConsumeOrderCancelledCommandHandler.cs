using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Services;
using KartInventoryService.Domain.Inventory;
using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderCancelled;

public sealed class ConsumeOrderCancelledCommandHandler : IRequestHandler<ConsumeOrderCancelledCommand>
{
    private const string SystemPrincipal = "system:inventory-order-cancelled-consumer";

    private readonly IReservationRepository _reservationRepository;
    private readonly ReservationReleaseService _releaseService;

    public ConsumeOrderCancelledCommandHandler(IReservationRepository reservationRepository, ReservationReleaseService releaseService)
    {
        _reservationRepository = reservationRepository;
        _releaseService = releaseService;
    }

    public async Task Handle(ConsumeOrderCancelledCommand request, CancellationToken cancellationToken)
    {
        var reserved = await _reservationRepository.GetReservedByOrderIdAsync(request.OrderId, cancellationToken);
        foreach (var reservation in reserved)
        {
            await _releaseService.ReleaseAsync(
                reservation.ReservationId,
                ReservationReleaseReason.OrderCancelled,
                SystemPrincipal,
                cancellationToken);
        }
    }
}
