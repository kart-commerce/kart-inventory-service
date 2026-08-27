using FluentValidation;

namespace KartInventoryService.Application.Features.ProvisionWarehouseStock;

public sealed class ProvisionWarehouseStockCommandValidator : AbstractValidator<ProvisionWarehouseStockCommand>
{
    public ProvisionWarehouseStockCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.InitialQty).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ReplenishmentThreshold).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TargetStockingLevel).GreaterThan(0);
    }
}
