namespace DocumentationBackend.Configuration;

public sealed class DocumentTemplatesInfrastructureOptions
{
    public const string SectionName = "DocumentTemplates";

    public MinioStorageOptions Minio { get; set; } = new();
    public AiTemplateOptions Ai { get; set; } = new();
}

public sealed class MinioStorageOptions
{
    /// <summary>URL du service S3-compatible (ex. http://localhost:9000).</summary>
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "documentation-templates";
    public string? Region { get; set; }
    /// <summary>Préfixe URL pour liens publics (ex. http://localhost:9000/documentation-templates).</summary>
    public string? PublicBaseUrl { get; set; }
}

