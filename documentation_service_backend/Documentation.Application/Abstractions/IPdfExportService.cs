namespace Documentation.Application.Abstractions;

public interface IPdfExportService
{
    (string FileName, byte[] PdfBytes) BuildPdf(
        string templateCode,
        string tenantId,
        string renderedContent,
        string? documentTitleFallback = null);
}
