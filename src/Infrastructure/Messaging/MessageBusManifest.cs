namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Strongly-typed mirror of contracts/message-bus-manifest.json - this service's entire RabbitMQ
/// topology (exchanges, queues, bindings, dead-letter/retry wiring). The manifest is the single
/// source of truth; nothing it describes may also be a hardcoded string literal elsewhere in
/// Infrastructure/Messaging. Mirrors kart-identity-service/kart-category-service exactly.
/// </summary>
public sealed record MessageBusManifest(
    string Service,
    IReadOnlyList<ExchangeDefinition> Exchanges,
    IReadOnlyList<ExchangeDefinition> ExternalExchanges,
    IReadOnlyList<PublishedEventDefinition> PublishedEvents,
    IReadOnlyList<QueueDefinition> Queues,
    IReadOnlyList<DeadLetterQueueDefinition> DeadLetterQueues)
{
    public string ExchangeFor(string eventType) => PublishedEventFor(eventType).Exchange;

    public string RoutingKeyFor(string eventType) => PublishedEventFor(eventType).RoutingKey;

    public QueueDefinition GetQueue(string name) =>
        Queues.FirstOrDefault(q => q.Name == name)
            ?? throw new InvalidOperationException($"message-bus-manifest.json has no queue named '{name}'.");

    private PublishedEventDefinition PublishedEventFor(string eventType) =>
        PublishedEvents.FirstOrDefault(e => e.EventType == eventType)
            ?? throw new InvalidOperationException($"message-bus-manifest.json has no publishedEvents entry for event type '{eventType}'.");
}

public sealed record ExchangeDefinition(string Name, string Type, bool Durable);
public sealed record PublishedEventDefinition(string EventType, string Exchange, string RoutingKey);
public sealed record QueueBindingDefinition(string Exchange, string RoutingKey);
public sealed record DeadLetterDefinition(string Exchange, string RoutingKey);
public sealed record RetryTierDefinition(string Name, int TtlMs);
public sealed record RetryLadderDefinition(string RequeueTo, IReadOnlyList<RetryTierDefinition> Tiers);
public sealed record QueueDefinition(
    string Name, bool Durable, IReadOnlyList<QueueBindingDefinition> Bindings,
    DeadLetterDefinition? DeadLetter, RetryLadderDefinition? RetryLadder);
public sealed record DeadLetterQueueDefinition(string Name, string Exchange, string RoutingKey);
