using System;
using System.Collections.Generic;

namespace Shared.Kernel; // ← corriger : enlever le doublon "Shared.Kernel."

public abstract class AggregateRoot : BaseEntity
{
    private readonly List<object> _domainEvents = new();
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();
    protected void AddDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}