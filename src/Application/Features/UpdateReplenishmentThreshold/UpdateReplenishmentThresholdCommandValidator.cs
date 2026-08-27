using FluentValidation;

namespace KartInventoryService.Application.Features.UpdateReplenishmentThreshold;

public sealed class UpdateReplenishmentThresholdCommandValidator : AbstractValidator<UpdateReplenishmentThresholdCommand>
{
    public UpdateReplenishmentThresholdCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.ReplenishmentThreshold).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TargetStockingLevel).GreaterThan(0);
    }
}
