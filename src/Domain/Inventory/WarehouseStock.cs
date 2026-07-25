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
    /// Initial-load provisioning only (ddd-model.md Modeling Decision 2; tickets.md's flagged
    /// gap: no admin endpoint/ticket exists for onboarding warehouse/SKU master data) - never
    /// reachable through a public API, only through a migration seed or an equivalent trusted
    /// initial-load process (database-design.md's created_by comment: "Admin operator or an
    /// initial-load process").
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

        return Result.Success(new WarehouseStock(warehouseId, sku, initialQty, replenishmentThreshold, targetStockingLevel, actingPrincipal, now));
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
        return Result.Success();
    }

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        UpdatedBy = actingPrincipal;
        UpdatedAt = now;
    }
}
