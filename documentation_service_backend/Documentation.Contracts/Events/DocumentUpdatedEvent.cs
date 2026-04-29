namespace Documentation.Contracts.Events;

/// <summary>
/// Événement d’intégration émis lorsqu’une ressource documentaire est mise à jour.
/// </summary>
public record DocumentUpdatedEvent(
    Guid DocumentId,
    string TenantId,
    string ResourceType,
    DateTimeOffset OccurredAt);
