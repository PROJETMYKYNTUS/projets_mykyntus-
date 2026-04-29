namespace DocumentationBackend.Application.Abstractions;

/// <summary>Résout la clé API IA active pour le tenant courant (filtre EF), sinon retourne null.</summary>
public interface IAiApiKeyResolver
{
    Task<string?> GetActiveApiKeyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetFallbackApiKeysAsync(CancellationToken cancellationToken = default);
}
