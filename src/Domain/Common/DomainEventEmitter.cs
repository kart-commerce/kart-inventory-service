namespace KartInventoryService.Domain.Common;

/// <summary>
/// Base for an aggregate that collects in-process domain events raised during a single unit of
/// work. Infrastructure translates these into inventory_outbox_events rows within the same
/// SaveChanges transaction - never dispatched via an in-memory bus directly. Deliberately has no
/// forced surrogate Id (unlike a conventional AggregateRoot base) because WarehouseStock's
/// natural key is the pair (WarehouseId, Sku), not a Guid - each aggregate below declares its own
/// identity.
/// </summary>
public abstract class DomainEventEmitter
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
