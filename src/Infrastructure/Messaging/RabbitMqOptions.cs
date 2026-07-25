namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Binds the "RabbitMq" configuration section. Deliberately holds only connection info -
/// everything topology-related (exchanges, routing keys, queues, DLQs, retry ladders) lives in
/// contracts/message-bus-manifest.json, not here.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
