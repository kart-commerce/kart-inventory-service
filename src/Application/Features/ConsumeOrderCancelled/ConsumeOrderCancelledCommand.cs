using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderCancelled;

/// <summary>
/// Dispatched by Infrastructure/Messaging/OrderEventsConsumerHostedService on receipt of Order's
/// OrderCancelled event (event-contract.md Consumed Events) - releases every still-`reserved`
/// reservation for this orderId.
/// </summary>
public sealed record ConsumeOrderCancelledCommand(Guid OrderId) : IRequest;
