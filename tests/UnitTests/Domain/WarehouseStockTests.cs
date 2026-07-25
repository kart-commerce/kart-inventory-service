using FluentAssertions;
using KartInventoryService.Domain.Inventory;
using Xunit;

namespace KartInventoryService.UnitTests.Domain;

public class WarehouseStockTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string ActingPrincipal = "system:test";

    private static WarehouseStock CreateStock(int availableQty = 100, int threshold = 20, int target = 100) =>
        WarehouseStock.Provision("WH-1", "SKU-1", availableQty, threshold, target, ActingPrincipal, Now).Value;

    [Fact]
    public void TryDebit_WhenSufficientStock_DecreasesAvailableQty()
    {
        var stock = CreateStock(availableQty: 50);

        var result = stock.TryDebit(30, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        stock.AvailableQty.Should().Be(20);
    }

    [Fact]
    public void TryDebit_WhenInsufficientStock_FailsAndLeavesQuantityUnchanged()
    {
        // This is the platform's canonical oversell-prevention invariant (requirement-spec.md S4) -
        // stock must never go negative under concurrent reservation attempts.
        var stock = CreateStock(availableQty: 10);

        var result = stock.TryDebit(11, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("insufficient_stock");
        stock.AvailableQty.Should().Be(10);
    }

    [Fact]
    public void TryDebit_WhenQtyIsZeroOrNegative_FailsValidation()
    {
        var stock = CreateStock();

        var result = stock.TryDebit(0, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Credit_IncreasesAvailableQty_AndRaisesNoDomainEvent()
    {
        var stock = CreateStock(availableQty: 50);

        var result = stock.Credit(10, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        stock.AvailableQty.Should().Be(60);
        stock.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Replenish_IncreasesAvailableQty_AndRaisesInventoryReplenishedDomainEvent()
    {
        var stock = CreateStock(availableQty: 10, threshold: 20, target: 100);

        var result = stock.Replenish(50, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        stock.AvailableQty.Should().Be(60);
        stock.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InventoryReplenishedDomainEvent>()
            .Which.Should().BeEquivalentTo(new { Sku = "SKU-1", QtyAdded = 50, WarehouseId = "WH-1" });
    }

    [Fact]
    public void Provision_WithNegativeInitialQty_FailsValidation()
    {
        var result = WarehouseStock.Provision("WH-1", "SKU-1", -1, 20, 100, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }
}
