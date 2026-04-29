namespace DocumentationBackend.Api;

public sealed class AiApiKeyListItemResponse
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string? Label { get; set; }
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = "";
    /// <summary>Suffixe masqué (ex. …x4Ab2) — jamais la clé complète.</summary>
    public string KeyPreview { get; set; } = "";
}

public sealed class CreateAiApiKeyRequest
{
    public string Provider { get; set; } = "openai";
    public string ApiKey { get; set; } = "";
    public string? Label { get; set; }
    /// <summary>Si true, désactive les autres clés du tenant et active celle-ci.</summary>
    public bool SetActive { get; set; } = true;
}

public sealed class DocumentRequestFieldValuesResponse
{
    public IReadOnlyDictionary<string, string> Values { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class PutDocumentRequestFieldValuesRequest
{
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
