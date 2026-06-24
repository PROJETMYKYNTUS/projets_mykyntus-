using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Documentation.Infrastructure.Services;

public sealed class AiApiKeyAdminAppService(
    DocumentationDbContext db,
    IDocumentationTenantAccessor tenantAccessor,
    ILogger<AiApiKeyAdminAppService> logger) : IAiApiKeyAdminAppService
{
    public async Task<List<AiApiKeyListItemResponse>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.AiApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        logger.LogDebug(
            "List AI keys tenant={Tenant} total={Total} active={Active}",
            tenantAccessor.ResolvedTenantId,
            rows.Count,
            rows.Count(k => k.IsActive));
        return rows.Select(MapListItem).ToList();
    }

    public async Task<AiApiKeyListItemResponse> CreateAsync(CreateAiApiKeyRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.ApiKey))
            throw new DocumentationApiException(400, "apiKey est obligatoire.");
        var provider = string.IsNullOrWhiteSpace(body.Provider) ? "openai" : body.Provider.Trim().ToLowerInvariant();
        if (provider.Length > 32)
            throw new DocumentationApiException(400, "provider trop long.");

        var tenant = tenantAccessor.ResolvedTenantId;
        var beforeRows = await db.AiApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        logger.LogDebug(
            "Create AI key before tenant={Tenant} provider={Provider} setActive={SetActive} beforeTotal={Total}",
            tenant,
            provider,
            body.SetActive,
            beforeRows.Count);
        if (body.SetActive)
        {
            var actives = await db.AiApiKeys.Where(k => k.IsActive).ToListAsync(ct).ConfigureAwait(false);
            foreach (var k in actives)
                k.IsActive = false;
        }

        var entity = new AiApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Provider = provider,
            Label = string.IsNullOrWhiteSpace(body.Label) ? null : body.Label.Trim(),
            ApiKey = body.ApiKey.Trim(),
            IsActive = body.SetActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.AiApiKeys.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        var afterRows = await db.AiApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "AI API key created id={KeyId} tenant={TenantId} active={IsActive} afterTotal={Total}",
            entity.Id,
            tenant,
            entity.IsActive,
            afterRows.Count);
        return MapListItem(entity);
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false)
            ?? throw new DocumentationApiException(404, "Clé introuvable.");
        var others = await db.AiApiKeys.Where(k => k.Id != id && k.IsActive).ToListAsync(ct).ConfigureAwait(false);
        foreach (var o in others)
            o.IsActive = false;
        key.IsActive = true;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("AI API key activated id={KeyId}", id);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false)
            ?? throw new DocumentationApiException(404, "Clé introuvable.");
        key.IsActive = false;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false)
            ?? throw new DocumentationApiException(404, "Clé introuvable.");
        db.AiApiKeys.Remove(key);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("AI API key deleted id={KeyId}", id);
    }

    private static AiApiKeyListItemResponse MapListItem(AiApiKey k)
    {
        var raw = k.ApiKey ?? "";
        var preview = raw.Length <= 4 ? "****" : "…" + raw[^4..];
        return new AiApiKeyListItemResponse
        {
            Id = k.Id.ToString(),
            Provider = k.Provider,
            Label = k.Label,
            IsActive = k.IsActive,
            CreatedAt = k.CreatedAt.ToString("O"),
            KeyPreview = preview,
        };
    }
}

public sealed class DocumentationWorkflowAppService(DocumentationWorkflowService inner) : IDocumentationWorkflowAppService
{
    public async Task<WorkflowOperationResult> ValidateAsync(Guid documentRequestId, string? comment, CancellationToken ct = default)
    {
        var (response, statusCode, error) = await inner.ValidateAsync(documentRequestId, comment, ct);
        return new WorkflowOperationResult(response, statusCode, error);
    }

    public async Task<WorkflowOperationResult> ApproveAsync(Guid documentRequestId, CancellationToken ct = default)
    {
        var (response, statusCode, error) = await inner.ApproveAsync(documentRequestId, ct);
        return new WorkflowOperationResult(response, statusCode, error);
    }

    public async Task<WorkflowOperationResult> RejectAsync(Guid documentRequestId, string rejectionReason, CancellationToken ct = default)
    {
        var (response, statusCode, error) = await inner.RejectAsync(documentRequestId, rejectionReason, ct);
        return new WorkflowOperationResult(response, statusCode, error);
    }
}

public sealed class AiDirectDocumentAppService(
    AiDirectDocumentFillOrchestrator aiDirectFill,
    IDocumentationTenantAccessor tenantAccessor,
    IPdfExportService pdfExport,
    IStructuredDocumentDocxExportService docxExport) : IAiDirectDocumentAppService
{
    public async Task<AiDirectDocumentFillResponse> GenerateAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default)
    {
        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.GenerateDocumentAiUi, ct);
        if (!outcome.Success)
            throw new DocumentationApiException(outcome.ErrorStatusCode, ExtractErrorMessage(outcome.ErrorBody));
        return new AiDirectDocumentFillResponse("ok", outcome.Document, null, false, null);
    }

    public async Task<FileExportResultDto> PreviewPdfAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default)
    {
        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.GenerateDocumentAiUi, ct);
        if (!outcome.Success)
            throw new DocumentationApiException(outcome.ErrorStatusCode, ExtractErrorMessage(outcome.ErrorBody));

        var title = string.IsNullOrWhiteSpace(body.DocumentTitle) ? "Aperçu" : body.DocumentTitle.Trim();
        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            "IA_DIRECT",
            tenantAccessor.ResolvedTenantId,
            outcome.Document!,
            title);
        return new FileExportResultDto(pdfBytes, "application/pdf", fileName);
    }

    public Task<FileExportResultDto> ExportAsync(AiDirectRenderRequest body, CancellationToken ct = default)
    {
        var text = body.Document?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new DocumentationApiException(400, "document est obligatoire.");

        var fmt = (body.Format ?? "pdf").Trim().ToLowerInvariant();
        var title = string.IsNullOrWhiteSpace(body.Title) ? "Document" : body.Title.Trim();

        if (fmt == "pdf")
        {
            var (fileName, pdfBytes) = pdfExport.BuildPdf("IA_EXPORT", tenantAccessor.ResolvedTenantId, text, title);
            return Task.FromResult(new FileExportResultDto(pdfBytes, "application/pdf", fileName));
        }

        if (fmt == "docx")
        {
            var bytes = docxExport.Build(title, text, string.Empty, "Officiel");
            return Task.FromResult(new FileExportResultDto(
                bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "document_ia.docx"));
        }

        throw new DocumentationApiException(400, "Format non pris en charge (pdf, docx).");
    }

    public async Task<AiDirectDocumentFillResponse> FillValidatedAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default)
    {
        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.Full, ct);
        if (!outcome.Success)
            throw new DocumentationApiException(outcome.ErrorStatusCode, ExtractErrorMessage(outcome.ErrorBody), outcome.ErrorBody);
        return new AiDirectDocumentFillResponse("ok", outcome.Document, null, false);
    }

    public async Task<TemplateFileExportDto> PreviewValidatedPdfAsync(AiDirectDocumentFillRequest body, CancellationToken ct = default)
    {
        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.Full, ct);
        if (!outcome.Success)
            throw new DocumentationApiException(outcome.ErrorStatusCode, ExtractErrorMessage(outcome.ErrorBody), outcome.ErrorBody);

        var title = string.IsNullOrWhiteSpace(body.DocumentTitle) ? "Aperçu" : body.DocumentTitle.Trim();
        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            "AI_DIRECT_PREVIEW",
            tenantAccessor.ResolvedTenantId,
            outcome.Document!,
            title);
        return new TemplateFileExportDto(
            pdfBytes,
            "application/pdf",
            fileName,
            new Dictionary<string, string>
            {
                ["Cache-Control"] = "no-store, no-cache, must-revalidate",
                ["Pragma"] = "no-cache",
            });
    }

    private static string ExtractErrorMessage(object errorBody)
    {
        if (errorBody is { } o)
        {
            var messageProp = o.GetType().GetProperty("message");
            if (messageProp?.GetValue(o) is string msg && !string.IsNullOrWhiteSpace(msg))
                return msg;
        }
        return "Opération impossible.";
    }
}

public sealed class StructuredDocumentDocxExportService : IStructuredDocumentDocxExportService
{
    public byte[] Build(string title, string mainText, string signatureText, string watermark) =>
        StructuredDocumentDocxExporter.Build(title, mainText, signatureText, watermark);
}
