namespace DocumentationBackend.Application.Abstractions;

/// <summary>Contenu binaire lu depuis MinIO/S3 (prévisualisation / téléchargement via l’API).</summary>
public sealed record TemplateBlobPayload(byte[] Content, string ContentType, string FileName);

/// <summary>Stockage objet S3-compatible (MinIO, AWS S3).</summary>
public interface ITemplateBlobStorage
{
    bool IsConfigured { get; }

    /// <summary>Envoie le fichier et retourne l’URL publique ou de référence.</summary>
    Task<string> PutTemplateObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// URL GET signée (accès navigateur) si <paramref name="storedObjectUri"/> correspond à ce dépôt (path-style /bucket/key).
    /// Sinon <c>null</c> (URI absente, hôte différent, stockage non configuré).
    /// </summary>
    string? TryGetPresignedGetUrl(string? storedObjectUri, TimeSpan lifetime);

    /// <summary>
    /// Lit l’objet référencé par l’URL stockée (même parsing que le lien présigné). Limite de taille pour éviter d’épuiser la RAM.
    /// </summary>
    Task<TemplateBlobPayload?> TryReadObjectAsync(string? storedObjectUri, CancellationToken cancellationToken = default);
}
