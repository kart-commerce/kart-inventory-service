namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>event-contract.md Consumed Events: OrderCompensationTriggered (payload orderId, reason), published by kart-order-service (BRD S12.2; ADR-0007).</summary>
public sealed record OrderCompensationTriggeredEventPayload(Guid OrderId, string Reason);
