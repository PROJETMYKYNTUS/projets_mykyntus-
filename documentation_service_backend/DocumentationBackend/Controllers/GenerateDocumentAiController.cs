using DocumentationBackend.Api;
using DocumentationBackend.Context;
using DocumentationBackend.Data;
using DocumentationBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentationBackend.Controllers;

/// <summary>API dédiée UI « Génération de documents » (IA directe, sans moteur local).</summary>
[ApiController]
[Route("api")]
public sealed class GenerateDocumentAiController(
    DocumentationUserContext userContext,
    IDocumentationTenantAccessor tenantAccessor,
    AiDirectDocumentFillOrchestrator aiDirectFill,
    IPdfExportService pdfExport) : ControllerBase
{
    [HttpPost("generate-document-ai")]
    public async Task<IActionResult> Generate([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.GenerateDocumentAiUi, ct);
        if (!outcome.Success)
            return StatusCode(outcome.ErrorStatusCode, outcome.ErrorBody);

        return Ok(new AiDirectDocumentFillResponse("ok", outcome.Document, null, false, null));
    }

    /// <summary>Même logique que <see cref="Generate"/> puis rendu PDF (un seul aller-retour OpenAI).</summary>
    [HttpPost("generate-document-ai/preview")]
    public async Task<IActionResult> PreviewPdf([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var outcome = await aiDirectFill.FillAsync(body, AiDirectFillValidationPolicy.GenerateDocumentAiUi, ct);
        if (!outcome.Success)
            return StatusCode(outcome.ErrorStatusCode, outcome.ErrorBody);

        var title = string.IsNullOrWhiteSpace(body.DocumentTitle) ? "Aperçu" : body.DocumentTitle.Trim();
        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            "IA_DIRECT",
            tenantAccessor.ResolvedTenantId,
            outcome.Document!,
            title);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        return File(pdfBytes, "application/pdf", fileDownloadName: fileName);
    }

    /// <summary>PDF ou DOCX à partir du texte déjà généré (pas d’appel OpenAI).</summary>
    [HttpPost("generate-document-ai/export")]
    public IActionResult Export([FromBody] AiDirectRenderRequest body)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var text = body.Document?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return BadRequest(new { message = "document est obligatoire." });

        var fmt = (body.Format ?? "pdf").Trim().ToLowerInvariant();
        var title = string.IsNullOrWhiteSpace(body.Title) ? "Document" : body.Title.Trim();

        if (fmt == "pdf")
        {
            var (fileName, pdfBytes) = pdfExport.BuildPdf("IA_EXPORT", tenantAccessor.ResolvedTenantId, text, title);
            return File(pdfBytes, "application/pdf", fileDownloadName: fileName);
        }

        if (fmt == "docx")
        {
            var bytes = StructuredDocumentDocxExporter.Build(title, text, string.Empty, "Officiel");
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileDownloadName: "document_ia.docx");
        }

        return BadRequest(new { message = "Format non pris en charge (pdf, docx)." });
    }
}
