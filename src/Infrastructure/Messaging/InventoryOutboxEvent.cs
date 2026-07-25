using System.Text.Json;
using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// database-design.md `inventory_outbox_events` - one row per InventoryReserved/
/// InventoryReservationFailed/InventoryReleased/InventoryReplenished, written in the same
/// transaction as the write it describes (design-decisions.md, "Event Publish Atomicity").
/// Purely an Infrastructure/messaging concern - Domain only raises the three aggregate-attached
/// domain events (InventoryReserved/InventoryReleased on Reservation, InventoryReplenished on
/// WarehouseStock); InventoryReservationFailed is never attached to an aggregate and is created
/// directly via <see cref="Create"/> by OutboxEventWriter instead.
/// </summary>
public sealed class InventoryOutboxEvent
{
    public const string InventoryReservedEventType = "InventoryReserved";
    public const string InventoryReservationFailedEventType = "InventoryReservationFailed";
    public const string InventoryReleasedEventType = "InventoryReleased";
    public const string InventoryReplenishedEventType = "InventoryReplenished";

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
    };

    public static InventoryOutboxEvent ForReserved(InventoryReservedDomainEvent domainEvent, Guid reservationId, string actingPrincipal) =>
        Create(InventoryReservedEventType, reservationId.ToString(), new { orderId = domainEvent.OrderId, sku = domainEvent.Sku, qty = domainEvent.Qty }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForReleased(InventoryReleasedDomainEvent domainEvent, Guid reservationId, string actingPrincipal) =>
        Create(InventoryReleasedEventType, reservationId.ToString(), new { orderId = domainEvent.OrderId, sku = domainEvent.Sku, qty = domainEvent.Qty }, domainEvent.OccurredAt, actingPrincipal);

    public static InventoryOutboxEvent ForReplenished(InventoryReplenishedDomainEvent domainEvent, string aggregateRef, string actingPrincipal) =>
        Create(InventoryReplenishedEventType, aggregateRef, new { sku = domainEvent.Sku, qtyAdded = domainEvent.QtyAdded, warehouseId = domainEvent.WarehouseId }, domainEvent.OccurredAt, actingPrincipal);

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
