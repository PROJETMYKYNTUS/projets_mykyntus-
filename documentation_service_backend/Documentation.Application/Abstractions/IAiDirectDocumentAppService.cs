using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IAiDirectDocumentAppService
{
    Task<AiDirectDocumentFillResponse> GenerateAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default);
    Task<FileExportResultDto> PreviewPdfAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default);
    Task<FileExportResultDto> ExportAsync(AiDirectRenderRequest body, CancellationToken ct = default);

    /// <summary>Validation stricte (placeholders + données critiques) — workflow documentation/data.</summary>
    Task<AiDirectDocumentFillResponse> FillValidatedAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default);

    /// <summary>Aperçu PDF avec la même validation que <see cref="FillValidatedAsync"/>.</summary>
    Task<TemplateFileExportDto> PreviewValidatedPdfAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default);
}
