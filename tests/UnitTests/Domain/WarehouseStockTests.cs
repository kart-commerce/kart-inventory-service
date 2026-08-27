using FluentAssertions;
using KartInventoryService.Domain.Inventory;
using Xunit;

namespace KartInventoryService.UnitTests.Domain;

public class WarehouseStockTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string ActingPrincipal = "system:test";

    private static WarehouseStock CreateStock(int availableQty = 100, int threshold = 20, int target = 100)
    {
        var stock = WarehouseStock.Provision("WH-1", "SKU-1", availableQty, threshold, target, ActingPrincipal, Now).Value;

        // Provision itself now raises WarehouseStockProvisionedDomainEvent - these tests are
        // about the action taken *after* provisioning, so clear the setup event.
        stock.ClearDomainEvents();
        return stock;
    }

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

    [Fact]
    public void Provision_RaisesWarehouseStockProvisionedDomainEvent()
    {
        var result = WarehouseStock.Provision("WH-1", "SKU-1", 10, 5, 50, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WarehouseStockProvisionedDomainEvent>()
            .Which.Should().BeEquivalentTo(new { WarehouseId = "WH-1", Sku = "SKU-1", InitialQty = 10 });
    }

    [Fact]
    public void TryDebit_WhenResultLeavesQtyBelowThreshold_AlsoRaisesLowStockDetected()
    {
        var stock = CreateStock(availableQty: 25, threshold: 20);

        var result = stock.TryDebit(10, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        stock.AvailableQty.Should().Be(15);
        stock.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LowStockDetectedDomainEvent>()
            .Which.Should().BeEquivalentTo(new { WarehouseId = "WH-1", Sku = "SKU-1", AvailableQty = 15, Threshold = 20 });
    }

    [Fact]
    public void TryDebit_WhenResultStaysAboveThreshold_RaisesNoLowStockEvent()
    {
        var stock = CreateStock(availableQty: 50, threshold: 20);

        stock.TryDebit(10, ActingPrincipal, Now);

        stock.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateThreshold_PersistsNewValues_AndRaisesNoDomainEvent()
    {
        var stock = CreateStock(threshold: 20, target: 100);

        var result = stock.UpdateThreshold(5, 50, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        stock.ReplenishmentThreshold.Should().Be(5);
        stock.TargetStockingLevel.Should().Be(50);
        stock.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateThreshold_WithNonPositiveTarget_FailsValidation()
    {
        var stock = CreateStock();

        var result = stock.UpdateThreshold(5, 0, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Reconcile_WithHigherCount_SetsAvailableQty_AndReturnsPositiveVariance()
    {
        var stock = CreateStock(availableQty: 40, threshold: 10);

        var result = stock.Reconcile(55, "cycle-count", ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(15);
        stock.AvailableQty.Should().Be(55);
        stock.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InventoryReconciledDomainEvent>()
            .Which.Should().BeEquivalentTo(new { WarehouseId = "WH-1", Sku = "SKU-1", PreviousQty = 40, CountedQty = 55, Variance = 15, Reason = "cycle-count" });
    }

    [Fact]
    public void Reconcile_WithLowerCount_SetsAvailableQty_AndReturnsNegativeVariance_AndMayRaiseLowStock()
    {
        var stock = CreateStock(availableQty: 40, threshold: 10);

        var result = stock.Reconcile(5, "shrinkage", ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(-35);
        stock.AvailableQty.Should().Be(5);
        stock.DomainEvents.Should().HaveCount(2);
        stock.DomainEvents.OfType<InventoryReconciledDomainEvent>().Should().ContainSingle();
        stock.DomainEvents.OfType<LowStockDetectedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Reconcile_WithEmptyReason_FailsValidation()
    {
        var stock = CreateStock();

        var result = stock.Reconcile(10, string.Empty, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }
}
