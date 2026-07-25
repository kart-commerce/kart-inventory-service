using FluentValidation;

namespace KartInventoryService.Application.Features.ReserveStock;

/// <summary>api-contract.yaml reserveStock requestBody: required: [orderId, sku, qty], qty minimum 1.</summary>
public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.Qty).GreaterThan(0);
    }
}
