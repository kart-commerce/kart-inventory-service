using KartInventoryService.Application.Common.Services;
using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderConfirmed;

public sealed class ConsumeOrderConfirmedCommandHandler : IRequestHandler<ConsumeOrderConfirmedCommand>
{
    private const string SystemPrincipal = "system:inventory-order-confirmed-consumer";

    private readonly ReservationCommitService _commitService;

    public ConsumeOrderConfirmedCommandHandler(ReservationCommitService commitService)
    {
        _commitService = commitService;
    }

    public Task Handle(ConsumeOrderConfirmedCommand request, CancellationToken cancellationToken) =>
        _commitService.CommitOrderAsync(request.OrderId, SystemPrincipal, cancellationToken);
}
