using System.Diagnostics;
using System.Text.Json;
using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// database-design.md `inventory_outbox_events` - one row per published Inventory event, written
/// in the same transaction as the write it describes (design-decisions.md, "Event Publish
/// Atomicity"). Purely an Infrastructure/messaging concern - Domain only raises the aggregate-
/// attached domain events (Reservation: InventoryReserved/InventoryReleased/InventoryCommitted;
/// WarehouseStock: InventoryReplenished/LowStockDetected/InventoryReconciled/
/// WarehouseStockProvisioned); InventoryReservationFailed is never attached to an aggregate and is
/// created directly via <see cref="Create"/> by OutboxEventWriter instead.
/// </summary>
public sealed class InventoryOutboxEvent
{
    public const string InventoryReservedEventType = "InventoryReserved";
    public const string InventoryReservationFailedEventType = "InventoryReservationFailed";
    public const string InventoryReleasedEventType = "InventoryReleased";
    public const string InventoryReplenishedEventType = "InventoryReplenished";

    /// <summary>Inventory & Stock Management flow's "Deduct (Order Confirmed)" stage.</summary>
    public const string InventoryCommittedEventType = "InventoryCommitted";

    /// <summary>Inventory & Stock Management flow's "Reorder Alert" stage.</summary>
    public const string LowStockDetectedEventType = "LowStockDetected";

    /// <summary>Inventory & Stock Management flow's "Stock Audit/Reconciliation" and "Update Qty" stages.</summary>
    public const string InventoryReconciledEventType = "InventoryReconciled";

    /// <summary>Inventory & Stock Management flow's new-SKU-onboarding gap closure.</summary>
    public const string WarehouseStockProvisionedEventType = "WarehouseStockProvisioned";

    private const string RelaySystemPrincipal = "system:inventory-outbox-relay";

    /// <summary>event-contract.md's field names are camelCase (orderId, qtyAdded, ...).</summary>
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string AggregateRef { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = RelaySystemPrincipal;

    /// <summary>
    /// W3C traceparent captured from Activity.Current at the moment this row was created - the
    /// originating request's trace, persisted so OutboxRelayHostedService (a background poller,
    /// seconds later, on an unrelated async context) can continue that same trace rather than
    /// starting a disconnected new one. Null for rows written before this column existed.
    /// </summary>
    public string? TraceParent { get; private set; }

    private InventoryOutboxEvent()
    {
    }

    public static InventoryOutboxEvent Create(string eventType, string aggregateRef, object payload, DateTimeOffset occurredAt, string actingPrincipal) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = eventType,
        AggregateRef = aggregateRef,
        Payload = JsonSerializer.Serialize(payload, PayloadSerializerOptions),
        OccurredAt = occurredAt,
        CreatedBy = actingPrincipal,
        TraceParent = Activity.Current?.Id,
    };

    public static InventoryOutboxEvent ForReserved(InventoryReservedDomainEvent domainEvent, Guid reservationId, string actingPrincipal) =>
        Create(InventoryReservedEventType, reservationId.ToString(), new { orderId = domainEvent.OrderId, sku = domainEvent.Sku, qty = domainEvent.Qty }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForReleased(InventoryReleasedDomainEvent domainEvent, Guid reservationId, string actingPrincipal) =>
        Create(InventoryReleasedEventType, reservationId.ToString(), new { orderId = domainEvent.OrderId, sku = domainEvent.Sku, qty = domainEvent.Qty }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForReplenished(InventoryReplenishedDomainEvent domainEvent, string aggregateRef, string actingPrincipal) =>
        Create(InventoryReplenishedEventType, aggregateRef, new { sku = domainEvent.Sku, qtyAdded = domainEvent.QtyAdded, warehouseId = domainEvent.WarehouseId }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForCommitted(InventoryCommittedDomainEvent domainEvent, Guid reservationId, string actingPrincipal) =>
        Create(InventoryCommittedEventType, reservationId.ToString(), new { orderId = domainEvent.OrderId, sku = domainEvent.Sku, qty = domainEvent.Qty }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForLowStockDetected(LowStockDetectedDomainEvent domainEvent, string actingPrincipal) =>
        Create(LowStockDetectedEventType, $"{domainEvent.WarehouseId}:{domainEvent.Sku}", new { warehouseId = domainEvent.WarehouseId, sku = domainEvent.Sku, availableQty = domainEvent.AvailableQty, threshold = domainEvent.Threshold }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForReconciled(InventoryReconciledDomainEvent domainEvent, string actingPrincipal) =>
        Create(InventoryReconciledEventType, $"{domainEvent.WarehouseId}:{domainEvent.Sku}", new { warehouseId = domainEvent.WarehouseId, sku = domainEvent.Sku, previousQty = domainEvent.PreviousQty, countedQty = domainEvent.CountedQty, variance = domainEvent.Variance, reason = domainEvent.Reason }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForProvisioned(WarehouseStockProvisionedDomainEvent domainEvent, string actingPrincipal) =>
        Create(WarehouseStockProvisionedEventType, $"{domainEvent.WarehouseId}:{domainEvent.Sku}", new { warehouseId = domainEvent.WarehouseId, sku = domainEvent.Sku, initialQty = domainEvent.InitialQty }, domainEvent.OccurredAt, actingPrincipal);

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
