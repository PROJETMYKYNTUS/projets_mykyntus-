using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IDocumentWorkflowGenerationAppService
{
    Task<TemplateFileExportDto> PreviewDocumentAsync(DocumentWorkflowRequest req, CancellationToken ct = default);

    Task<DocumentTemplateGenerateResponse> GenerateDocumentAsync(DocumentWorkflowRequest req, CancellationToken ct = default);

    Task<DocumentTemplateGenerateResponse> UploadReadyDocumentAsync(UploadReadyDocumentCommand dto, CancellationToken ct = default);
}

public sealed record UploadReadyDocumentCommand(
    byte[] FileBytes,
    string FileName,
    string ContentType,
    Guid? DocumentRequestId = null,
    Guid? BeneficiaryUserId = null,
    Guid? DocumentTypeId = null);
