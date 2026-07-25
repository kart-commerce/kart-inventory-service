namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>event-contract.md Consumed Events: OrderCancelled, published by kart-order-service.</summary>
public sealed record OrderCancelledEventPayload(Guid OrderId);
