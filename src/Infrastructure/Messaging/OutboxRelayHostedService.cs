using System.Text;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using KartInventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Relays inventory_outbox_events rows to inventory.exchange (design-decisions.md, "Event Publish
/// Atomicity"). Re-declares the manifest's topology idempotently on every (re)connect. Connects
/// lazily with its own retry loop so a RabbitMQ outage degrades publish latency, never crashes the
/// Api process. Mirrors kart-category-service's OutboxRelayHostedService exactly. Every service's
/// outbox event belongs to this one flow (unlike admin-service's multi-flow relay, which needs a
/// per-action map) - the flow tag is unconditional. Publishes under
/// StartPublishActivityFromStoredTraceParent so the relay - a background poller, seconds later, on
/// an async context wholly unrelated to the original request - continues that request's trace
/// rather than starting a disconnected new one.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private const string FlowName = "InventoryStockManagement";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<OutboxRelayHostedService> logger)
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

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OutboxRelayHostedService>>();

        var pending = await dbContext.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var outboxEvent in pending)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.EventId.ToString();
            properties.ContentType = "application/json";

            var exchange = _manifest.ExchangeFor(outboxEvent.EventType);
            var routingKey = _manifest.RoutingKeyFor(outboxEvent.EventType);

            using var activity = RabbitMqTraceContext.StartPublishActivityFromStoredTraceParent(
                exchange, routingKey, outboxEvent.TraceParent, properties);

            channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(DateTimeOffset.UtcNow);

            logger.LogInformation(
                "Stage {Stage}: {EventType} published to {Exchange}/{RoutingKey} (event {EventId}).",
                "OutboxEventPublished",
                outboxEvent.EventType,
                exchange,
                routingKey,
                outboxEvent.EventId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
