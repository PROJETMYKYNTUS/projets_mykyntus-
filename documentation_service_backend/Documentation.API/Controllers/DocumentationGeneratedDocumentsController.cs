using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.GeneratedDocuments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Documents générés — téléchargement, export multi-format et workflow éditeur RH.</summary>
[ApiController]
[Route("api/documentation/data")]
public sealed class DocumentationGeneratedDocumentsController(
    IMediator mediator,
    IGeneratedDocumentAppService? generated) : ControllerBase
{
    [HttpGet("generated-documents/{id:guid}/file")]
    public async Task<IActionResult> DownloadGeneratedDocumentFile(Guid id, CancellationToken ct)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new DownloadGeneratedDocumentFileQuery(id), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpGet("generated-documents/{id:guid}/rh-editor")]
    public async Task<IActionResult> GetRhGeneratedDocumentEditor(Guid id, CancellationToken ct)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetRhGeneratedDocumentEditorQuery(id), ct));
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPut("generated-documents/{id:guid}/rh-editor")]
    public async Task<IActionResult> PutRhGeneratedDocumentEditor(
        Guid id,
        [FromBody] UpdateRhGeneratedDocumentContentRequest body,
        CancellationToken ct)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            await mediator.Send(new PutRhGeneratedDocumentEditorCommand(id, body), ct);
            return NoContent();
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpPost("generated-documents/{id:guid}/finalize-rh")]
    public async Task<IActionResult> FinalizeRhGeneratedDocument(Guid id, CancellationToken ct)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new FinalizeRhGeneratedDocumentCommand(id), ct));
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpGet("generated-documents/{id:guid}/export")]
    public async Task<IActionResult> ExportGeneratedDocument(Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new ExportGeneratedDocumentQuery(id, format, ClientContext()), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    [HttpGet("documents/{id:guid}/download")]
    public Task<IActionResult> DownloadDocument(Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default) =>
        ExportGeneratedDocument(id, format, ct);

    [HttpGet("document-requests/{requestId:guid}/download")]
    public async Task<IActionResult> DownloadDocumentRequestExport(
        Guid requestId,
        [FromQuery] string format = "pdf",
        CancellationToken ct = default)
    {
        if (generated is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new DownloadDocumentRequestExportQuery(requestId, format, ClientContext()), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return MapExceptionResult(ex); }
    }

    private GeneratedDocumentClientContext ClientContext() =>
        new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

    private IActionResult MapExceptionResult(DocumentationApiException ex)
    {
        var body = ex.Payload ?? new { message = ex.Message };
        return ex.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => Unauthorized(),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(body),
            StatusCodes.Status422UnprocessableEntity => UnprocessableEntity(body),
            _ => StatusCode(ex.StatusCode, body),
        };
    }
}
