using DocumentationBackend.Api;
using Microsoft.AspNetCore.Http;

namespace DocumentationBackend.Application.DocumentTemplates;

public interface IDocumentTemplateManagementService
{
    /// <summary>Multipart : fichier → MinIO + template UPLOAD (dynamique ou statique).</summary>
    Task<DocumentTemplateDetailResponse> CreateFromUploadedFileAsync(
        Guid userId,
        string code,
        string name,
        string? description,
        Guid? documentTypeId,
        IFormFile file,
        bool staticDocument = false,
        bool requiresPilotUpload = false,
        CancellationToken cancellationToken = default);

    /// <summary>Description → IA → template AI_GENERATED.</summary>
    Task<DocumentTemplateDetailResponse> CreateFromAiDescriptionAsync(
        Guid userId,
        string description,
        string? name,
        string? code,
        Guid? documentTypeId,
        CancellationToken cancellationToken = default);

    Task<DocumentTemplateDetailResponse> CreateFromInternalEngineAsync(
        Guid userId,
        string code,
        string name,
        string? description,
        Guid? documentTypeId,
        string structuredContent,
        IReadOnlyList<TemplateVariableInput>? variables,
        CancellationToken cancellationToken = default);
}
