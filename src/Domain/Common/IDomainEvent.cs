namespace KartInventoryService.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
