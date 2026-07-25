using FluentValidation;

namespace KartInventoryService.Application.Features.ReleaseReservation;

public sealed class ReleaseReservationCommandValidator : AbstractValidator<ReleaseReservationCommand>
{
    public ReleaseReservationCommandValidator()
    {
        RuleFor(c => c.ReservationId).NotEmpty();
    }
}
