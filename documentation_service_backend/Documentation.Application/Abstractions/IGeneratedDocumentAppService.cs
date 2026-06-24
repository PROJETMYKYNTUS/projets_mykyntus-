using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IGeneratedDocumentAppService
{
    Task<FileExportResultDto> DownloadGeneratedDocumentFileAsync(Guid id, CancellationToken ct = default);

    Task<RhGeneratedDocumentEditorResponse> GetRhGeneratedDocumentEditorAsync(Guid id, CancellationToken ct = default);

    Task PutRhGeneratedDocumentEditorAsync(
        Guid id,
        UpdateRhGeneratedDocumentContentRequest body,
        CancellationToken ct = default);

    Task<DocumentTemplateGenerateResponse> FinalizeRhGeneratedDocumentAsync(Guid id, CancellationToken ct = default);

    Task<FileExportResultDto> ExportGeneratedDocumentAsync(
        Guid id,
        string format = "pdf",
        GeneratedDocumentClientContext? clientContext = null,
        CancellationToken ct = default);

    Task<FileExportResultDto> DownloadDocumentRequestExportAsync(
        Guid requestId,
        string format = "pdf",
        GeneratedDocumentClientContext? clientContext = null,
        CancellationToken ct = default);
}
