using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Infrastructure.Persistence;

namespace KartInventoryService.Infrastructure.Messaging;

/// <summary>
/// Implements IOutboxEventWriter for InventoryReservationFailed - the one published event that is
/// never attached to a persisted aggregate. Commits its own row independently via a plain
/// SaveChangesAsync call (the caller has typically just rolled back the failed reservation
/// transaction, so no ambient transaction is active here).
/// </summary>
public sealed class OutboxEventWriter : IOutboxEventWriter
{
    private readonly InventoryDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public OutboxEventWriter(InventoryDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task EnqueueAsync(string eventType, string aggregateRef, object payload, string actingPrincipal, CancellationToken cancellationToken)
    {
        var occurredAt = _timeProvider.GetUtcNow();
        var outboxEvent = InventoryOutboxEvent.Create(eventType, aggregateRef, payload, occurredAt, actingPrincipal);
        _dbContext.OutboxEvents.Add(outboxEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
