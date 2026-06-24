using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Mapping;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Documentation.Infrastructure.Services;

public sealed class GeneratedDocumentAppService(
    DocumentationDbContext db,
    IDocumentationTenantAccessor tenantAccessor,
    IDocumentationRequestContext userContext,
    IDocumentRequestAppService documentRequests,
    IDocumentTemplateVariableMergeService variableMerge,
    ITemplateBlobStorage templateBlobStorage,
    IPdfExportService pdfExport,
    IOriginalDocxTemplateRenderService originalDocxTemplateRender,
    ITemplateEngineService templateEngine,
    ILogger<GeneratedDocumentAppService> logger) : IGeneratedDocumentAppService
{
    private static readonly Regex LegacyMissingMarkerRegex = new(
        @"\(\s*X+\s*\)|\(\s*\)|\(\s+\)|(?<![A-Za-z0-9])X(?![A-Za-z0-9])|_{1,}|(?<![A-Za-z0-9])(?:-|\u2014)(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<FileExportResultDto> DownloadGeneratedDocumentFileAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var gen = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");

        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");

        if (gen.Status == GeneratedDocumentStatus.DraftPendingRhReview)
        {
            throw new DocumentationApiException(
                409,
                "Brouillon RH : aucun fichier final tant que la validation RH n'est pas terminée.",
                new
                {
                    needsRhEditorReview = true,
                    generatedDocumentId = gen.Id.ToString("D"),
                });
        }

        byte[]? bytes = gen.PdfContent;
        if (bytes is null || bytes.Length == 0)
        {
            var payload = await templateBlobStorage.TryReadObjectAsync(gen.StorageUri, ct);
            if (payload is null || payload.Content.Length == 0)
                throw new DocumentationApiException(404, "Fichier binaire introuvable (MinIO ou base).");
            bytes = payload.Content;
        }

        var downloadName = string.IsNullOrWhiteSpace(gen.FileName) ? "document.pdf" : gen.FileName;
        var contentType = string.IsNullOrWhiteSpace(gen.MimeType) ? "application/pdf" : gen.MimeType;
        return new FileExportResultDto(bytes, contentType, downloadName);
    }

    public async Task<RhGeneratedDocumentEditorResponse> GetRhGeneratedDocumentEditorAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        EnsureRhOrAdmin();

        var gen = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            throw new DocumentationApiException(400, "Ce document n'est pas un brouillon en attente de validation RH.");

        var missing = DeserializeRhMissingVariables(gen.RhMissingVariablesJson);
        var generated = gen.ContentGenerated ?? string.Empty;
        var editable = string.IsNullOrEmpty(gen.ContentFinal) ? generated : gen.ContentFinal!;
        return new RhGeneratedDocumentEditorResponse(
            gen.Id.ToString("D"),
            gen.Status == GeneratedDocumentStatus.DraftPendingRhReview ? "InProgress" : gen.Status.ToString(),
            generated,
            editable,
            missing);
    }

    public async Task PutRhGeneratedDocumentEditorAsync(
        Guid id,
        UpdateRhGeneratedDocumentContentRequest body,
        CancellationToken ct = default)
    {
        EnsureAuthenticated();
        EnsureRhOrAdmin();

        var gen = await db.GeneratedDocuments.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            throw new DocumentationApiException(400, "Ce document n'est pas un brouillon modifiable.");

        gen.ContentFinal = body.Content ?? string.Empty;
        gen.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DocumentTemplateGenerateResponse> FinalizeRhGeneratedDocumentAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        EnsureRhOrAdmin();

        var gen = await db.GeneratedDocuments
            .Include(g => g.DocumentRequest)
            .Include(g => g.DocumentType)
            .Include(g => g.TemplateVersion)!.ThenInclude(v => v!.Template)!.ThenInclude(t => t!.DocumentType)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            throw new DocumentationApiException(400, "Ce document n'est pas un brouillon en attente de finalisation.");

        var rendered = gen.ContentFinal?.Trim();
        if (string.IsNullOrEmpty(rendered))
            throw new DocumentationApiException(400, "Contenu vide : complétez le texte avant de valider le document.");

        var unresolvedMarkers = DetectUnresolvedLegacyMarkers(rendered);
        if (unresolvedMarkers.Count > 0)
        {
            throw new DocumentationApiException(
                400,
                "Données manquantes : certains champs du document contiennent encore des marqueurs vides. Complétez les données avant validation finale.",
                new { missingVariables = unresolvedMarkers });
        }

        var template = gen.TemplateVersion?.Template;
        if (template is null)
            throw new DocumentationApiException(400, "Modèle introuvable pour ce document.");

        var titleFallback = gen.DocumentType?.Name
            ?? template.DocumentType?.Name
            ?? template.Name
            ?? template.Code;

        var now = DateTimeOffset.UtcNow;
        var exportContext = await TryBuildStructuredExportContextAsync(gen, ct);
        var originalAssetUri = exportContext?.Version?.OriginalAssetUri;
        var originalDocx = await templateBlobStorage.TryReadObjectAsync(originalAssetUri, ct);
        if (exportContext is not null &&
            DocxTemplatePayloadInspector.IsWordProcessingOpenXml(originalDocx) &&
            originalDocx is { Content.Length: > 0 })
        {
            var docxBytes = originalDocxTemplateRender.Render(originalDocx.Content, exportContext.Value.Merged);
            var stem = await BuildExportFileStemAsync(gen, ct);
            var docxFileName = $"{stem}.docx";
            string storageUri;
            if (templateBlobStorage.IsConfigured)
            {
                await using var stream = new MemoryStream(docxBytes);
                var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{gen.Id:N}/{docxFileName}";
                storageUri = await templateBlobStorage.PutTemplateObjectAsync(
                    key,
                    stream,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ct);
            }
            else
            {
                storageUri = $"inline://generated/{gen.Id:N}/{Uri.EscapeDataString(docxFileName)}";
            }

            gen.FileName = docxFileName;
            gen.StorageUri = storageUri;
            gen.PdfContent = templateBlobStorage.IsConfigured ? null : docxBytes;
            gen.MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            gen.FileSizeBytes = docxBytes.LongLength;
            gen.Status = GeneratedDocumentStatus.Generated;
            gen.UpdatedAt = now;
        }
        else
        {
            var (fileName, pdfBytes) = pdfExport.BuildPdf(
                template.Code,
                tenantAccessor.ResolvedTenantId,
                rendered,
                titleFallback);

            string storageUri;
            if (templateBlobStorage.IsConfigured)
            {
                await using var stream = new MemoryStream(pdfBytes);
                var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{gen.Id:N}/{fileName}";
                storageUri = await templateBlobStorage.PutTemplateObjectAsync(key, stream, "application/pdf", ct);
            }
            else
            {
                storageUri = $"inline://generated/{gen.Id:N}/{Uri.EscapeDataString(fileName)}";
            }

            gen.FileName = fileName;
            gen.StorageUri = storageUri;
            gen.PdfContent = templateBlobStorage.IsConfigured ? null : pdfBytes;
            gen.MimeType = "application/pdf";
            gen.FileSizeBytes = pdfBytes.LongLength;
            gen.Status = GeneratedDocumentStatus.Generated;
            gen.UpdatedAt = now;
        }

        if (gen.DocumentRequest is { } linkedRequest)
        {
            linkedRequest.Status = DocumentRequestStatus.Generated;
            linkedRequest.UpdatedAt = now;
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                fileName = gen.FileName,
                templateCode = template.Code,
                templateId = template.Id.ToString("D"),
                rhFinalized = true,
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_GENERATED",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);
        return new DocumentTemplateGenerateResponse(gen.Id.ToString("D"), gen.FileName, gen.StorageUri, gen.Status.ToString());
    }

    public Task<FileExportResultDto> ExportGeneratedDocumentAsync(
        Guid id,
        string format = "pdf",
        GeneratedDocumentClientContext? clientContext = null,
        CancellationToken ct = default) =>
        ExportGeneratedDocumentCoreAsync(id, format, clientContext, ct);

    public async Task<FileExportResultDto> DownloadDocumentRequestExportAsync(
        Guid requestId,
        string format = "pdf",
        GeneratedDocumentClientContext? clientContext = null,
        CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null)
            throw new DocumentationApiException(404, "Demande introuvable.");

        if (!await documentRequests.CanActorViewAsync(req, ct))
            throw new DocumentationApiException(403, "Accès refusé.");

        var latest = await DocumentRequestMappingHelper.LoadLatestGeneratedForRequestAsync(db, requestId, ct);
        if (latest is null)
            throw new DocumentationApiException(404, "Aucun document généré pour cette demande.");

        var gen = await LoadGeneratedDocumentForExportAsync(latest.Id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");

        return await ExportGeneratedDocumentCoreAsync(gen, format, clientContext, ct);
    }

    private async Task<FileExportResultDto> ExportGeneratedDocumentCoreAsync(
        Guid id,
        string formatRaw,
        GeneratedDocumentClientContext? clientContext,
        CancellationToken ct)
    {
        EnsureAuthenticated();

        var gen = await LoadGeneratedDocumentForExportAsync(id, ct);
        if (gen is null)
            throw new DocumentationApiException(404, "Document généré introuvable.");

        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            throw new DocumentationApiException(403, "Accès refusé.");

        return await ExportGeneratedDocumentCoreAsync(gen, formatRaw, clientContext, ct);
    }

    private async Task<FileExportResultDto> ExportGeneratedDocumentCoreAsync(
        GeneratedDocument gen,
        string formatRaw,
        GeneratedDocumentClientContext? clientContext,
        CancellationToken ct)
    {
        var format = NormalizeExportFormat(formatRaw);
        if (format is null)
            throw new DocumentationApiException(400, "Format non pris en charge (pdf, docx, txt, html).");

        if (gen.Status == GeneratedDocumentStatus.DraftPendingRhReview)
        {
            throw new DocumentationApiException(
                409,
                "Document en cours de finalisation RH.",
                new
                {
                    needsRhEditorReview = true,
                    generatedDocumentId = gen.Id.ToString("D"),
                    status = "InProgress",
                });
        }

        byte[] bytes;
        string contentType;
        string ext;

        if (format == "pdf")
        {
            var officialMime = (gen.MimeType ?? string.Empty).ToLowerInvariant();
            if (officialMime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentationApiException(
                    422,
                    "Le document officiel est le fichier Word (mise en page identique au modèle uploadé). Utilisez l'export DOCX. Une conversion PDF fidèle au modèle nécessite un moteur serveur (ex. LibreOffice) non branché ici.",
                    new { officialFormat = "docx" });
            }

            var pdf = await TryGetStoredPdfBytesAsync(gen, ct);
            if (pdf is null)
                throw new DocumentationApiException(404, "Fichier PDF introuvable (MinIO ou base).");
            bytes = pdf;
            contentType = "application/pdf";
            ext = ".pdf";
        }
        else
        {
            var exportContext = await TryBuildStructuredExportContextAsync(gen, ct);
            if (exportContext is null)
                throw new DocumentationApiException(400, "Export indisponible : version de modèle ou contenu structuré absent.");

            var rendered = await TryRenderStructuredExportAsync(gen, ct);
            if (rendered is null)
                throw new DocumentationApiException(400, "Export indisponible : rendu introuvable.");

            var parts = StructuredDocumentExportParser.Parse(rendered.Value.Rendered, rendered.Value.TitleFallback);
            const string watermark = "Officiel";
            switch (format)
            {
                case "docx":
                {
                    var genMime = (gen.MimeType ?? string.Empty).ToLowerInvariant();
                    if (gen.Status == GeneratedDocumentStatus.Generated &&
                        genMime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
                    {
                        var officialBytes = await TryGetStoredPdfBytesAsync(gen, ct);
                        if (officialBytes is { Length: > 0 })
                        {
                            bytes = officialBytes;
                            contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                            ext = ".docx";
                            break;
                        }
                    }

                    var originalAssetUri = exportContext.Value.Version?.OriginalAssetUri;
                    var originalDocx = await templateBlobStorage.TryReadObjectAsync(originalAssetUri, ct);

                    if (DocxTemplatePayloadInspector.IsWordProcessingOpenXml(originalDocx) && originalDocx is not null && originalDocx.Content.Length > 0)
                    {
                        bytes = originalDocxTemplateRender.Render(originalDocx.Content, exportContext.Value.Merged);
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        ext = ".docx";
                    }
                    else
                    {
                        bytes = StructuredDocumentDocxExporter.Build(parts.Title, parts.MainText, parts.SignatureText, watermark);
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        ext = ".docx";
                    }

                    break;
                }
                case "txt":
                    bytes = StructuredDocumentHtmlTxtExporter.BuildTxtUtf8(parts, watermark);
                    contentType = "text/plain; charset=utf-8";
                    ext = ".txt";
                    break;
                case "html":
                    bytes = StructuredDocumentHtmlTxtExporter.BuildHtmlUtf8(parts.Title, parts, watermark);
                    contentType = "text/html; charset=utf-8";
                    ext = ".html";
                    break;
                default:
                    throw new DocumentationApiException(400, "Format non pris en charge.");
            }
        }

        var stem = await BuildExportFileStemAsync(gen, ct);
        var fileName = $"{stem}{ext}";
        await LogDocumentDownloadAsync(gen, format, fileName, clientContext, ct);
        return new FileExportResultDto(bytes, contentType, fileName);
    }

    private async Task<GeneratedDocument?> LoadGeneratedDocumentForExportAsync(Guid id, CancellationToken ct)
    {
        return await db.GeneratedDocuments.AsNoTracking()
            .Include(g => g.TemplateVersion)
                .ThenInclude(v => v!.Template)
            .Include(g => g.DocumentType)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    private static string? NormalizeExportFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "pdf";
        return format.Trim().ToLowerInvariant() switch
        {
            "pdf" => "pdf",
            "docx" => "docx",
            "txt" => "txt",
            "html" => "html",
            _ => null,
        };
    }

    private async Task<byte[]?> TryGetStoredPdfBytesAsync(GeneratedDocument gen, CancellationToken ct)
    {
        byte[]? bytes = gen.PdfContent;
        if (bytes is null || bytes.Length == 0)
        {
            var payload = await templateBlobStorage.TryReadObjectAsync(gen.StorageUri, ct);
            if (payload is null || payload.Content.Length == 0)
                return null;
            bytes = payload.Content;
        }

        return bytes;
    }

    private async Task<Dictionary<string, string>> BuildExportMergedDictionaryAsync(
        GeneratedDocument gen,
        DocumentTemplateVersion version,
        string titleFallback,
        CancellationToken ct)
    {
        var fromSnap = TryParseWorkflowVariablesSnapshot(gen.WorkflowVariablesSnapshotJson);
        if (fromSnap is not null)
            return fromSnap;

        Guid? beneficiaryId = null;
        if (gen.DocumentRequestId is { } drId)
        {
            var reqRow = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == drId, ct);
            beneficiaryId = reqRow?.BeneficiaryUserId ?? reqRow?.RequesterUserId;
        }

        var merged = await variableMerge.MergeAsync(beneficiaryId, gen.DocumentRequestId, null, ct);
        var d = gen.CreatedAt;
        merged["date"] = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        merged["date_fr"] = d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        variableMerge.EnsureFrenchDateAlias(merged);
        await variableMerge.ApplyAiRefinementAsync(merged, version.Id, titleFallback, ct).ConfigureAwait(false);
        return merged;
    }

    private static Dictionary<string, string>? TryParseWorkflowVariablesSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (d is null || d.Count == 0)
                return null;
            return new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<(string Rendered, string TitleFallback)?> TryRenderStructuredExportAsync(GeneratedDocument gen, CancellationToken ct)
    {
        var version = gen.TemplateVersion;
        if (version is null && gen.TemplateVersionId is { } tvId)
        {
            version = await db.DocumentTemplateVersions.AsNoTracking()
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == tvId, ct);
        }

        var titleFallback = gen.DocumentType?.Name
            ?? version?.Template?.Name
            ?? version?.Template?.Code
            ?? "Document";

        if (gen.Status == GeneratedDocumentStatus.Generated && !string.IsNullOrWhiteSpace(gen.ContentFinal))
            return (gen.ContentFinal!, titleFallback);

        if (version is null || string.IsNullOrWhiteSpace(version.StructuredContent))
            return null;

        var merged = await BuildExportMergedDictionaryAsync(gen, version, titleFallback, ct);
        var rendered = templateEngine.RenderContent(version.StructuredContent, merged);
        return (rendered, titleFallback);
    }

    private async Task<(Dictionary<string, string> Merged, DocumentTemplateVersion? Version, string TitleFallback)?> TryBuildStructuredExportContextAsync(
        GeneratedDocument gen,
        CancellationToken ct)
    {
        var version = gen.TemplateVersion;
        if (version is null && gen.TemplateVersionId is { } tvId)
        {
            version = await db.DocumentTemplateVersions.AsNoTracking()
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == tvId, ct);
        }

        var titleFallback = gen.DocumentType?.Name
            ?? version?.Template?.Name
            ?? version?.Template?.Code
            ?? "Document";

        if (version is null)
            return null;

        var merged = await BuildExportMergedDictionaryAsync(gen, version, titleFallback, ct);
        return (merged, version, titleFallback);
    }

    private async Task<string> BuildExportFileStemAsync(GeneratedDocument gen, CancellationToken ct)
    {
        static string SlugifySegment(string s)
        {
            var cleaned = string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Replace(" ", "_", StringComparison.Ordinal);
            while (cleaned.Contains("__", StringComparison.Ordinal))
                cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(cleaned))
                return "document";
            return cleaned.Length > 64 ? cleaned[..64] : cleaned;
        }

        var typePart = "document";
        if (gen.DocumentType is { } dt && !string.IsNullOrWhiteSpace(dt.Code))
            typePart = SlugifySegment(dt.Code);
        else if (gen.TemplateVersion?.Template is { } tpl && !string.IsNullOrWhiteSpace(tpl.Code))
            typePart = SlugifySegment(tpl.Code);

        var namePart = "beneficiaire";
        if (gen.DocumentRequestId is { } rid)
        {
            var req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct);
            if (req is not null)
            {
                var uid = req.BeneficiaryUserId ?? req.RequesterUserId;
                var du = await db.DirectoryUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
                if (du is not null)
                {
                    var raw = $"{du.Prenom}{du.Nom}".Trim();
                    if (raw.Length > 0)
                        namePart = SlugifySegment(raw);
                }
            }
        }

        return $"{typePart}_{namePart}";
    }

    private async Task LogDocumentDownloadAsync(
        GeneratedDocument gen,
        string format,
        string fileName,
        GeneratedDocumentClientContext? clientContext,
        CancellationToken ct)
    {
        DocumentRequest? req = null;
        if (gen.DocumentRequestId is { } rid)
            req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct);

        var details = JsonSerializer.Serialize(new
        {
            format,
            fileName,
            generatedDocumentId = gen.Id.ToString("D"),
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantAccessor.ResolvedTenantId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = userContext.UserId,
            Action = "DOCUMENT_DOWNLOAD",
            EntityType = req is not null ? "document_request" : "generated_document",
            EntityId = req?.Id ?? gen.Id,
            Details = details,
            Success = true,
            RequestNumber = req?.RequestNumber,
            IpAddress = TryParseIpAddress(clientContext?.RemoteIpAddress),
            UserAgent = clientContext?.UserAgent,
        });
        await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<string> DeserializeRhMissingVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is null || list.Count == 0)
                return Array.Empty<string>();
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private async Task<bool> IsGeneratedDocumentInTenantAsync(GeneratedDocument gen, CancellationToken ct)
    {
        if (gen.DocumentRequestId is not { } rid)
            return true;
        var tenant = tenantAccessor.ResolvedTenantId.Trim();
        var rowTenant = await db.DocumentRequests.AsNoTracking()
            .Where(r => r.Id == rid)
            .Select(r => r.TenantId)
            .FirstOrDefaultAsync(ct);
        return string.Equals((rowTenant ?? string.Empty).Trim(), tenant, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> DetectUnresolvedLegacyMarkers(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<string>();
        var markers = LegacyMissingMarkerRegex.Matches(content)
            .Select(m => m.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        return markers;
    }

    private async Task<bool> CanAccessGeneratedDocumentAsync(GeneratedDocument g, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return false;
        if (g.OwnerUserId == userContext.UserId.Value)
            return true;
        if (userContext.Role.Value is AppRole.Rh or AppRole.Admin or AppRole.Audit)
            return true;

        if (!g.DocumentRequestId.HasValue)
            return false;

        var req = await db.DocumentRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == g.DocumentRequestId.Value, ct);
        if (req is null)
            return false;

        return await documentRequests.CanActorViewAsync(req, ct);
    }

    private static System.Net.IPAddress? TryParseIpAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && System.Net.IPAddress.TryParse(value, out var ip) ? ip : null;

    private void EnsureAuthenticated()
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            throw new DocumentationApiException(401, "Authentification requise.");
    }

    private void EnsureRhOrAdmin()
    {
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            throw new DocumentationApiException(403, "Accès refusé.");
    }
}
