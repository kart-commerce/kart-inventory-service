using FluentValidation;

namespace KartInventoryService.Application.Features.ReconcileStock;

public sealed class ReconcileStockCommandValidator : AbstractValidator<ReconcileStockCommand>
{
    public ReconcileStockCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.CountedQty).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Reason).NotEmpty();
    }
}
