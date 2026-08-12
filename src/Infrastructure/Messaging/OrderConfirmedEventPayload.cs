namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>Consumed from order-service's OrderConfirmed (order.exchange, routing key order.order.confirmed) - Inventory & Stock Management flow's "Deduct (Order Confirmed)" stage.</summary>
public sealed record OrderConfirmedEventPayload(Guid OrderId);
