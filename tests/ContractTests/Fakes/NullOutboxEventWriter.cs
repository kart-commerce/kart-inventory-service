using KartInventoryService.Application.Common.Interfaces;

namespace KartInventoryService.ContractTests.Fakes;

public sealed class NullOutboxEventWriter : IOutboxEventWriter
{
    public Task EnqueueAsync(string eventType, string aggregateRef, object payload, string actingPrincipal, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
