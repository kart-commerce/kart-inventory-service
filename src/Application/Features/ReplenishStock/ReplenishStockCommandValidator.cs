using FluentValidation;

namespace KartInventoryService.Application.Features.ReplenishStock;

public sealed class ReplenishStockCommandValidator : AbstractValidator<ReplenishStockCommand>
{
    public ReplenishStockCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.QtyAdded).GreaterThan(0);
    }
}
