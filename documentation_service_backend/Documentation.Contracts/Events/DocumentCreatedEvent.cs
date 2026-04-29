namespace Documentation.Contracts.Events;

/// <summary>
/// Événement d’intégration émis lorsqu’une ressource documentaire est créée (ex. demande, document généré).
/// </summary>
/// <param name="DocumentId">Identifiant métier de la ressource créée.</param>
/// <param name="TenantId">Identifiant tenant d’isolation.</param>
/// <param name="ResourceType">Discriminant du type de ressource (ex. <c>document_request</c>, <c>generated_document</c>).</param>
/// <param name="OccurredAt">Horodatage UTC de l’événement.</param>
public record DocumentCreatedEvent(
    Guid DocumentId,
    string TenantId,
    string ResourceType,
    DateTimeOffset OccurredAt);
