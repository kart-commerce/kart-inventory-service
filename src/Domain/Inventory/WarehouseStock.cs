using KartInventoryService.Domain.Common;

namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// ddd-model.md aggregate: identified by (WarehouseId, Sku) - this natural-key pair IS the row
/// every reservation/release/replenishment write locks (SELECT ... FOR UPDATE,
/// design-decisions.md's "Concurrency Control Pattern for Stock Rows"). The oversell invariant -
/// AvailableQty must never go negative under concurrent writers - is enforced here in
/// <see cref="TryDebit"/>; the actual serialization is the caller's job (repository takes the row
/// lock before calling into this aggregate).
/// </summary>
public sealed class WarehouseStock : DomainEventEmitter
{
    public string WarehouseId { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public int AvailableQty { get; private set; }
    public int ReplenishmentThreshold { get; private set; }
    public int TargetStockingLevel { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    // EF Core materialization only.
    private WarehouseStock()
    {
    }

    private WarehouseStock(
        string warehouseId,
        string sku,
        int initialQty,
        int replenishmentThreshold,
        int targetStockingLevel,
        string actingPrincipal,
        DateTimeOffset now)
    {
        WarehouseId = warehouseId;
        Sku = sku;
        AvailableQty = initialQty;
        ReplenishmentThreshold = replenishmentThreshold;
        TargetStockingLevel = targetStockingLevel;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    /// <summary>
    /// Onboards a brand-new (WarehouseId, Sku) row. Originally "never reachable through a public
    /// API, only through a migration seed" (tickets.md's flagged gap) - the Inventory &amp; Stock
    /// Management flow closed that gap with a real AdminOnly-gated endpoint
    /// (ProvisionWarehouseStockCommand); this factory itself is unchanged except that it now
    /// raises WarehouseStockProvisioned so the onboarding is visible on the broker, not just in
    /// the database.
    /// </summary>
    public static Result<WarehouseStock> Provision(
        string warehouseId,
        string sku,
        int initialQty,
        int replenishmentThreshold,
        int targetStockingLevel,
        string actingPrincipal,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(warehouseId) || string.IsNullOrWhiteSpace(sku))
        {
            return Result.Failure<WarehouseStock>(Error.Validation("warehouseId and sku are required."));
        }

        if (initialQty < 0 || replenishmentThreshold < 0 || targetStockingLevel <= 0)
        {
            return Result.Failure<WarehouseStock>(Error.Validation("Stock quantities/thresholds must be non-negative (target must be positive)."));
        }

        var stock = new WarehouseStock(warehouseId, sku, initialQty, replenishmentThreshold, targetStockingLevel, actingPrincipal, now);
        stock.Raise(new WarehouseStockProvisionedDomainEvent(warehouseId, sku, initialQty, now));
        return Result.Success(stock);
    }

    /// <summary>
    /// requirement-spec.md S4 oversell invariant: fails rather than allowing AvailableQty to go
    /// negative. Caller must already hold this row's SELECT ... FOR UPDATE lock inside the
    /// current transaction - this method only enforces the invariant, it does not itself
    /// serialize concurrent callers.
    /// </summary>
    public Result TryDebit(int qty, string actingPrincipal, DateTimeOffset now)
    {
        if (qty <= 0)
        {
            return Result.Failure(Error.Validation("qty must be positive."));
        }

        if (AvailableQty < qty)
        {
            return Result.Failure(Error.InsufficientStock(
                $"Warehouse '{WarehouseId}' has only {AvailableQty} unit(s) of '{Sku}' available; {qty} requested."));
        }

        AvailableQty -= qty;
        Touch(actingPrincipal, now);
        RaiseLowStockIfNeeded(now);
        return Result.Success();
    }

    /// <summary>Release credit-back (explicit call, OrderCancelled, OrderCompensationTriggered, or the TTL sweep) - raises no event; InventoryReleased is raised on the Reservation aggregate.</summary>
    public Result Credit(int qty, string actingPrincipal, DateTimeOffset now)
    {
        if (qty <= 0)
        {
            return Result.Failure(Error.Validation("qty must be positive."));
        }

        AvailableQty += qty;
        Touch(actingPrincipal, now);
        return Result.Success();
    }

    /// <summary>
    /// requirement-spec.md Decision 5: manual admin-initiated or threshold-triggered replenishment,
    /// both through this identical write path. Raises InventoryReplenished.
    /// </summary>
    public Result Replenish(int qtyAdded, string actingPrincipal, DateTimeOffset now)
    {
        if (qtyAdded <= 0)
        {
            return Result.Failure(Error.Validation("qtyAdded must be positive."));
        }

        AvailableQty += qtyAdded;
        Touch(actingPrincipal, now);
        Raise(new InventoryReplenishedDomainEvent(Sku, qtyAdded, WarehouseId, now));
        RaiseLowStockIfNeeded(now);
        return Result.Success();
    }

    /// <summary>
    /// Inventory &amp; Stock Management flow's "Low Stock Threshold" stage: admin-adjustable
    /// threshold/target, previously set only at Provision time with no update path. Config-only
    /// change - raises no domain event (nothing downstream needs to react to a threshold change
    /// itself; a subsequent write crossing the new threshold will raise LowStockDetected as usual).
    /// </summary>
    public Result UpdateThreshold(int replenishmentThreshold, int targetStockingLevel, string actingPrincipal, DateTimeOffset now)
    {
        if (replenishmentThreshold < 0 || targetStockingLevel <= 0)
        {
            return Result.Failure(Error.Validation("Threshold must be non-negative and target stocking level must be positive."));
        }

        ReplenishmentThreshold = replenishmentThreshold;
        TargetStockingLevel = targetStockingLevel;
        Touch(actingPrincipal, now);
        return Result.Success();
    }

    /// <summary>
    /// Inventory &amp; Stock Management flow's "Stock Audit/Reconciliation" stage (also serves as
    /// this flow's "Update Qty" stage - the one write path that sets AvailableQty to an absolute
    /// counted value rather than debiting/crediting it). Unlike TryDebit, a physical recount is
    /// legitimately allowed to move AvailableQty in either direction, including matching what
    /// TryDebit's oversell guard would otherwise reject - reconciliation is the authoritative
    /// correction, not a demand that competes with the oversell invariant. Returns the signed
    /// variance (countedQty - previous AvailableQty) for the caller to surface.
    /// </summary>
    public Result<int> Reconcile(int countedQty, string reason, string actingPrincipal, DateTimeOffset now)
    {
        if (countedQty < 0)
        {
            return Result.Failure<int>(Error.Validation("countedQty must be non-negative."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<int>(Error.Validation("A reconciliation reason is required."));
        }

        var previousQty = AvailableQty;
        var variance = countedQty - previousQty;
        AvailableQty = countedQty;
        Touch(actingPrincipal, now);
        Raise(new InventoryReconciledDomainEvent(WarehouseId, Sku, previousQty, countedQty, variance, reason, now));
        RaiseLowStockIfNeeded(now);
        return Result.Success(variance);
    }

    /// <summary>
    /// requirement-spec.md Decision 5's threshold-based reorder signal, promoted from a LogWarning
    /// (Application-layer, now removed) to a real published event - Domain has zero framework
    /// deps so it can't log itself, but it can raise. Fires every time a write leaves AvailableQty
    /// below ReplenishmentThreshold, not only on the crossing - matches the pre-existing
    /// LogWarning's own unconditional-below-threshold semantics.
    /// </summary>
    private void RaiseLowStockIfNeeded(DateTimeOffset now)
    {
        if (AvailableQty < ReplenishmentThreshold)
        {
            Raise(new LowStockDetectedDomainEvent(WarehouseId, Sku, AvailableQty, ReplenishmentThreshold, now));
        }
    }

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        UpdatedBy = actingPrincipal;
        UpdatedAt = now;
    }
}
