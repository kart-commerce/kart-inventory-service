using FluentAssertions;
using KartInventoryService.Domain.Inventory;
using Xunit;

namespace KartInventoryService.UnitTests.Domain;

public class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private const string ActingPrincipal = "system:test";
    private static readonly Guid OrderId = Guid.NewGuid();

    private static Reservation CreateReservation(int qty = 5, IReadOnlyList<(string WarehouseId, int Qty)>? allocations = null) =>
        Reservation.Create(OrderId, "SKU-1", qty, allocations ?? new[] { ("WH-1", qty) }, Ttl, ActingPrincipal, Now).Value;

    [Fact]
    public void Create_WithAllocationsSummingToQty_SucceedsAndRaisesInventoryReserved()
    {
        var result = Reservation.Create(OrderId, "SKU-1", 10, new[] { ("WH-1", 6), ("WH-2", 4) }, Ttl, ActingPrincipal, Now);

        result.IsSuccess.Should().BeTrue();
        var reservation = result.Value;
        reservation.Status.Should().Be(ReservationStatus.Reserved);
        reservation.Allocations.Should().HaveCount(2);
        reservation.ExpiresAt.Should().Be(Now.Add(Ttl));
        reservation.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryReservedDomainEvent>();
    }

    [Fact]
    public void Create_WhenAllocationsDoNotSumToQty_FailsValidation()
    {
        var result = Reservation.Create(OrderId, "SKU-1", 10, new[] { ("WH-1", 6) }, Ttl, ActingPrincipal, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Release_FirstCall_TransitionsToReleased_AndRaisesInventoryReleased()
    {
        var reservation = CreateReservation();
        reservation.ClearDomainEvents();

        var result = reservation.Release(ReservationReleaseReason.ExplicitCall, ActingPrincipal, Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ReleaseOutcome.Released);
        reservation.Status.Should().Be(ReservationStatus.Released);
        reservation.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryReleasedDomainEvent>();
    }

    [Fact]
    public void Release_WithTtlExpiryReason_TransitionsToExpired_NotReleased()
    {
        var reservation = CreateReservation();

        var result = reservation.Release(ReservationReleaseReason.TtlExpiry, "system:inventory-ttl-sweep", Now.AddMinutes(16));

        result.Value.Should().Be(ReleaseOutcome.Released);
        reservation.Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public void Release_WhenAlreadyTerminal_IsIdempotentNoOp_AndDoesNotRaiseASecondEvent()
    {
        // design-decisions.md "Release Idempotency & State Machine Design": a redelivered/
        // duplicate release trigger against an already-terminal reservation must not
        // double-credit stock or re-publish InventoryReleased.
        var reservation = CreateReservation();
        reservation.Release(ReservationReleaseReason.ExplicitCall, ActingPrincipal, Now.AddMinutes(1));
        reservation.ClearDomainEvents();

        var secondResult = reservation.Release(ReservationReleaseReason.OrderCancelled, "system:inventory-order-cancelled-consumer", Now.AddMinutes(2));

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().Be(ReleaseOutcome.AlreadyTerminal);
        reservation.DomainEvents.Should().BeEmpty();
        // The reason recorded stays the first (explicit) release, not the second, no-op trigger.
        reservation.ReleaseReason.Should().Be(ReservationReleaseReason.ExplicitCall);
    }

    [Fact]
    public void Commit_WhenReserved_TransitionsToCommitted_AndRaisesInventoryCommitted()
    {
        var reservation = CreateReservation();
        reservation.ClearDomainEvents();

        var result = reservation.Commit(ActingPrincipal, Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Committed);
        reservation.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryCommittedDomainEvent>();
    }

    [Fact]
    public void Commit_WhenAlreadyCommitted_IsIdempotentNoOp_AndDoesNotRaiseASecondEvent()
    {
        var reservation = CreateReservation();
        reservation.Commit(ActingPrincipal, Now.AddMinutes(1));
        reservation.ClearDomainEvents();

        var result = reservation.Commit(ActingPrincipal, Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        reservation.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Release_WhenCommitted_StillTransitionsToReleased_AndCreditsBack()
    {
        // A paid (Committed) order can still be admin-cancelled later - Committed only opts a
        // reservation out of the TTL sweep, it must not become un-cancellable.
        var reservation = CreateReservation();
        reservation.Commit(ActingPrincipal, Now.AddMinutes(1));
        reservation.ClearDomainEvents();

        var result = reservation.Release(ReservationReleaseReason.OrderCancelled, "system:inventory-order-cancelled-consumer", Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ReleaseOutcome.Released);
        reservation.Status.Should().Be(ReservationStatus.Released);
        reservation.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryReleasedDomainEvent>();
    }

    [Fact]
    public void Commit_WhenAlreadyReleased_IsIdempotentNoOp()
    {
        var reservation = CreateReservation();
        reservation.Release(ReservationReleaseReason.ExplicitCall, ActingPrincipal, Now.AddMinutes(1));
        reservation.ClearDomainEvents();

        var result = reservation.Commit("system:inventory-order-confirmed-consumer", Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Released);
        reservation.DomainEvents.Should().BeEmpty();
    }
}
