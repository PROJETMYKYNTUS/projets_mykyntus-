using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.Documents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Workflow document — aperçu, génération, upload prêt et IA directe (données métier).</summary>
[ApiController]
[Route("api/documentation/data")]
public sealed class DocumentationDocumentsController(
    IMediator mediator,
    IDocumentWorkflowGenerationAppService? workflow,
    IAiDirectDocumentAppService? aiDirect) : ControllerBase
{
    [HttpPost("documents/preview")]
    public async Task<IActionResult> PreviewDocument([FromBody] DocumentWorkflowRequest req, CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new PreviewDocumentCommand(req), ct);
            ApplyResponseHeaders(file.ResponseHeaders);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPost("documents/ai-direct/fill")]
    public async Task<IActionResult> AiDirectFillDocument([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (aiDirect is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new AiDirectFillValidatedCommand(body), ct));
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPost("documents/ai-direct/preview")]
    public async Task<IActionResult> AiDirectPreviewPdf([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (aiDirect is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new AiDirectPreviewValidatedPdfCommand(body), ct);
            ApplyResponseHeaders(file.ResponseHeaders);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPost("documents/generate")]
    public async Task<IActionResult> GenerateDocumentWorkflow([FromBody] DocumentWorkflowRequest req, CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GenerateDocumentCommand(req), ct));
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPost("documents/upload-ready")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadReadyDocument(CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!Request.HasFormContentType)
            return BadRequest(new { message = "Formulaire multipart/form-data requis." });

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Fichier « file » requis." });
        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(new { message = "Fichier trop volumineux (max 50 Mo)." });

        Guid? requestId = null;
        var requestRaw = form["documentRequestId"].ToString();
        if (!string.IsNullOrWhiteSpace(requestRaw))
        {
            if (!Guid.TryParse(requestRaw, out var parsedReq) || parsedReq == Guid.Empty)
                return BadRequest(new { message = "documentRequestId invalide." });
            requestId = parsedReq;
        }

        Guid? beneficiaryId = null;
        var beneficiaryRaw = form["beneficiaryUserId"].ToString();
        if (!string.IsNullOrWhiteSpace(beneficiaryRaw))
        {
            if (!Guid.TryParse(beneficiaryRaw, out var parsedBeneficiary) || parsedBeneficiary == Guid.Empty)
                return BadRequest(new { message = "beneficiaryUserId invalide." });
            beneficiaryId = parsedBeneficiary;
        }

        Guid? explicitTypeId = null;
        var typeRaw = form["documentTypeId"].ToString();
        if (!string.IsNullOrWhiteSpace(typeRaw))
        {
            if (!Guid.TryParse(typeRaw, out var parsedType) || parsedType == Guid.Empty)
                return BadRequest(new { message = "documentTypeId invalide." });
            explicitTypeId = parsedType;
        }

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"document_pret_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}";

        try
        {
            return Ok(await mediator.Send(new UploadReadyDocumentRequest(
                bytes,
                fileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                requestId,
                beneficiaryId,
                explicitTypeId), ct));
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    private void ApplyResponseHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null) return;
        foreach (var (key, value) in headers)
            Response.Headers.Append(key, value);
    }

    private IActionResult MapExceptionResult(DocumentationApiException ex)
    {
        var body = ex.Payload ?? new { message = ex.Message };
        return ex.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => Unauthorized(),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(body),
            StatusCodes.Status409Conflict => Conflict(body),
            StatusCodes.Status422UnprocessableEntity => UnprocessableEntity(body),
            _ => StatusCode(ex.StatusCode, body),
        };
    }
}
