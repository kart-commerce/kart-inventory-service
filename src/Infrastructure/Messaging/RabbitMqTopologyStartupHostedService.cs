using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Declares this service's full <see cref="MessageBusManifest"/> topology once at startup,
/// registered ahead of <see cref="OutboxRelayHostedService"/> and
/// <see cref="OrderEventsConsumerHostedService"/> (the generic host runs
/// <see cref="IHostedService.StartAsync"/> in registration order) so the topology exists before
/// anything publishes or consumes. A RabbitMQ outage at boot must not crash the process - failure
/// here just logs; the relay/consumer each re-declare the same manifest idempotently inside their
/// own retrying connection anyway.
/// </summary>
public sealed class RabbitMqTopologyStartupHostedService : IHostedService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<RabbitMqTopologyStartupHostedService> _logger;

    public RabbitMqTopologyStartupHostedService(
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<RabbitMqTopologyStartupHostedService> logger)
    {
        _connectionFactory = connectionFactory;
        _manifest = manifest;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();
            RabbitMqTopologyProvisioner.Declare(channel, _manifest);
            _logger.LogInformation(
                "Declared RabbitMQ topology for {Service} from message-bus manifest ({ExchangeCount} exchange(s), {QueueCount} queue(s)).",
                _manifest.Service,
                _manifest.Exchanges.Count + _manifest.ExternalExchanges.Count,
                _manifest.Queues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not declare RabbitMQ topology for {Service} at startup; the outbox relay/consumer will retry this themselves once RabbitMQ is reachable.",
                _manifest.Service);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
