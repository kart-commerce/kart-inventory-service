namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Binds the "RabbitMq" configuration section. Deliberately holds only connection info -
/// everything topology-related (exchanges, routing keys, queues, DLQs, retry ladders) lives in
/// contracts/message-bus-manifest.json, not here.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// AMQP port. Defaults to RabbitMQ's standard 5672 - override when the broker is reached
    /// through a remapped host port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Dedicated non-guest broker credentials. RabbitMQ's default "guest" user is restricted to
    /// loopback-only connections, so any broker reached over a real network hop needs a real
    /// user. Left unset, RabbitMQ.Client falls back to its own guest/guest default — this pair
    /// was missing entirely until Order Management (Admin) flow #7's real end-to-end
    /// verification found this service's own RabbitMQ connections had therefore always been
    /// silently failing (ACCESS_REFUSED against the real broker's "kart" user, every consumer/
    /// outbox-relay connection attempt looping every 10s since this service's first deploy) —
    /// added to match every sibling service's own already-correct RabbitMqOptions shape.
    /// </summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
