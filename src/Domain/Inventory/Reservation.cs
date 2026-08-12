using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// ddd-model.md aggregate: the hold created by a POST /inventory/reserve call, correlated 1:1
/// with one orderId line-item request. ReservationId is the idempotency key every release
/// trigger (explicit call, OrderCancelled, OrderCompensationTriggered, TTL sweep) converges on.
/// </summary>
public sealed class Reservation : DomainEventEmitter
{
    private readonly List<WarehouseAllocation> _allocations = new();

    public Guid ReservationId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Qty { get; private set; }
    public ReservationStatus Status { get; private set; }
    public ReservationReleaseReason? ReleaseReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    public IReadOnlyList<WarehouseAllocation> Allocations => _allocations.AsReadOnly();

    // EF Core materialization only.
    private Reservation()
    {
    }

    private Reservation(
        Guid orderId,
        string sku,
        int qty,
        TimeSpan ttl,
        string actingPrincipal,
        DateTimeOffset now)
    {
        ReservationId = Guid.NewGuid();
        OrderId = orderId;
        Sku = sku;
        Qty = qty;
        Status = ReservationStatus.Reserved;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = now.Add(ttl);
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    /// <summary>
    /// requirement-spec.md S4: a reservation resolves to exactly one all-or-nothing allocation
    /// decision - allocations must already be computed (single-warehouse-first, or the
    /// multi-warehouse fallback) and their quantities must sum to qty before this is called; the
    /// caller (ReserveStockCommandHandler) is responsible for having already locked and debited
    /// every corresponding WarehouseStock row in the same transaction. Raises InventoryReserved.
    /// </summary>
    public static Result<Reservation> Create(
        Guid orderId,
        string sku,
        int qty,
        IReadOnlyList<(string WarehouseId, int Qty)> allocations,
        TimeSpan ttl,
        string actingPrincipal,
        DateTimeOffset now)
    {
        if (qty <= 0)
        {
            return Result.Failure<Reservation>(Error.Validation("qty must be positive."));
        }

        if (allocations.Count == 0)
        {
            return Result.Failure<Reservation>(Error.Validation("At least one warehouse allocation is required."));
        }

        if (allocations.Sum(a => a.Qty) != qty)
        {
            return Result.Failure<Reservation>(Error.Validation("Allocation quantities must sum to the requested qty."));
        }

        var reservation = new Reservation(orderId, sku, qty, ttl, actingPrincipal, now);
        foreach (var (warehouseId, allocatedQty) in allocations)
        {
            reservation._allocations.Add(new WarehouseAllocation(reservation.ReservationId, warehouseId, allocatedQty, actingPrincipal, now));
        }

        reservation.Raise(new InventoryReservedDomainEvent(orderId, sku, qty, now));
        return Result.Success(reservation);
    }

    /// <summary>
    /// design-decisions.md "Release Idempotency & State Machine Design": idempotent regardless of
    /// trigger. An already-terminal (Released/Expired) reservation is a no-op that still returns
    /// success - <see cref="ReleaseOutcome.AlreadyTerminal"/> - and must NOT cause the caller to
    /// credit WarehouseStock again or re-publish InventoryReleased. Reserved AND Committed are
    /// both releasable (a paid order can still be admin-cancelled - Committed only opts a
    /// reservation out of the TTL sweep, it does not make it un-cancellable). TtlExpiry
    /// transitions to Expired; every other reason transitions to Released (database-design.md's
    /// status/release_reason columns).
    /// </summary>
    public Result<ReleaseOutcome> Release(ReservationReleaseReason reason, string actingPrincipal, DateTimeOffset now)
    {
        if (Status == ReservationStatus.Released || Status == ReservationStatus.Expired)
        {
            return Result.Success(ReleaseOutcome.AlreadyTerminal);
        }

        Status = reason == ReservationReleaseReason.TtlExpiry ? ReservationStatus.Expired : ReservationStatus.Released;
        ReleaseReason = reason;
        ReleasedAt = now;
        Touch(actingPrincipal, now);
        Raise(new InventoryReleasedDomainEvent(OrderId, Sku, Qty, now));
        return Result.Success(ReleaseOutcome.Released);
    }

    /// <summary>
    /// Inventory &amp; Stock Management flow's "Deduct (Order Confirmed)" stage: consuming
    /// OrderConfirmed commits the reservation, opting it out of the TTL sweep. Idempotent - a
    /// reservation that is already Committed, or has since raced to Released/Expired under
    /// at-least-once delivery, is a silent no-op (never re-raises InventoryCommitted).
    /// </summary>
    public Result Commit(string actingPrincipal, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Reserved)
        {
            return Result.Success();
        }

        Status = ReservationStatus.Committed;
        Touch(actingPrincipal, now);
        Raise(new InventoryCommittedDomainEvent(OrderId, Sku, Qty, now));
        return Result.Success();
    }

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        UpdatedBy = actingPrincipal;
        UpdatedAt = now;
    }
}
