namespace Documentation.Contracts.Events;

/// <summary>
/// Événement d’intégration émis lorsqu’une ressource documentaire est supprimée ou archivée définitivement.
/// </summary>
public record DocumentDeletedEvent(
    Guid DocumentId,
    string TenantId,
    string ResourceType,
    DateTimeOffset OccurredAt);
