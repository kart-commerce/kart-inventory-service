using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderConfirmed;

/// <summary>
/// Dispatched by Infrastructure/Messaging/OrderEventsConsumerHostedService on receipt of Order's
/// OrderConfirmed event (order.exchange, routing key order.order.confirmed - Reserved -&gt; Paid on
/// order-service's own side) - Inventory &amp; Stock Management flow's "Deduct (Order Confirmed)"
/// stage. Commits every live reservation for the order.
/// </summary>
public sealed record ConsumeOrderConfirmedCommand(Guid OrderId) : IRequest;
