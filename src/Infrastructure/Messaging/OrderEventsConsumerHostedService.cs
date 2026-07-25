using System.Text;
using System.Text.Json;
using KartInventoryService.Application.Features.ConsumeOrderCancelled;
using KartInventoryService.Application.Features.ConsumeOrderCompensationTriggered;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Consumes inventory.order-events.queue (bound to order.exchange's order.order.cancelled and
/// order.order.compensation-triggered routing keys, per contracts/message-bus-manifest.json) and
/// dispatches to INV-3/INV-4 via MediatR. On handler failure, walks the manifest's retry ladder
/// (a custom retry-count header, since RabbitMQ has no built-in redelivery counter for this
/// pattern) and dead-letters once every tier is exhausted - the same shape as
/// kart-identity-service's UserDataErasedConsumerHostedService, generalized to switch on routing
/// key since this queue carries two distinct event types.
/// </summary>
public sealed class OrderEventsConsumerHostedService : BackgroundService
{
    private const string QueueName = "inventory.order-events.queue";
    private const string OrderCancelledRoutingKey = "order.order.cancelled";
    private const string OrderCompensationTriggeredRoutingKey = "order.order.compensation-triggered";
    private const string RetryCountHeader = "x-inventory-retry-count";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<OrderEventsConsumerHostedService> _logger;

    public OrderEventsConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<OrderEventsConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _manifest = manifest;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, _manifest);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, deliverEventArgs) => await OnMessageReceivedAsync(channel, deliverEventArgs, stoppingToken);
                channel.BasicConsume(QueueName, autoAck: false, consumer);

                while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory order-events consumer lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageReceivedAsync(IModel channel, BasicDeliverEventArgs deliverEventArgs, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var json = Encoding.UTF8.GetString(deliverEventArgs.Body.Span);

            switch (deliverEventArgs.RoutingKey)
            {
                case OrderCancelledRoutingKey:
                    var cancelled = JsonSerializer.Deserialize<OrderCancelledEventPayload>(json, SerializerOptions)
                        ?? throw new InvalidOperationException("OrderCancelled payload deserialized to null.");
                    await sender.Send(new ConsumeOrderCancelledCommand(cancelled.OrderId), stoppingToken);
                    break;

                case OrderCompensationTriggeredRoutingKey:
                    var compensation = JsonSerializer.Deserialize<OrderCompensationTriggeredEventPayload>(json, SerializerOptions)
                        ?? throw new InvalidOperationException("OrderCompensationTriggered payload deserialized to null.");
                    await sender.Send(new ConsumeOrderCompensationTriggeredCommand(compensation.OrderId, compensation.Reason), stoppingToken);
                    break;

                default:
                    _logger.LogWarning(
                        "Unrecognized routing key {RoutingKey} on {Queue}; dead-lettering.",
                        deliverEventArgs.RoutingKey,
                        QueueName);
                    channel.BasicNack(deliverEventArgs.DeliveryTag, multiple: false, requeue: false);
                    return;
            }

            channel.BasicAck(deliverEventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            HandleFailure(channel, deliverEventArgs, ex);
        }
    }

    private void HandleFailure(IModel channel, BasicDeliverEventArgs deliverEventArgs, Exception ex)
    {
        var retryCount = GetRetryCount(deliverEventArgs.BasicProperties);
        var tiers = _manifest.GetQueue(QueueName).RetryLadder?.Tiers ?? Array.Empty<RetryTierDefinition>();

        if (retryCount < tiers.Count)
        {
            var tier = tiers[retryCount];
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.Headers = new Dictionary<string, object> { [RetryCountHeader] = retryCount + 1 };

            // Publishing via the default exchange with routingKey = the retry-tier queue's own
            // name delivers directly to it; its TTL + dead-letter-back-to-main-queue wiring
            // (RabbitMqTopologyProvisioner) does the actual delayed redelivery.
            channel.BasicPublish(exchange: string.Empty, routingKey: tier.Name, basicProperties: properties, body: deliverEventArgs.Body);
            channel.BasicAck(deliverEventArgs.DeliveryTag, multiple: false);

            _logger.LogWarning(
                ex,
                "Handling {RoutingKey} failed; routed to retry tier {Tier} (attempt {Attempt}).",
                deliverEventArgs.RoutingKey,
                tier.Name,
                retryCount + 1);
        }
        else
        {
            _logger.LogCritical(
                ex,
                "Handling {RoutingKey} failed after exhausting all retry tiers; dead-lettering.",
                deliverEventArgs.RoutingKey);
            channel.BasicNack(deliverEventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static int GetRetryCount(IBasicProperties properties)
    {
        if (properties.Headers is not null && properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                _ => 0,
            };
        }

        return 0;
    }
}
