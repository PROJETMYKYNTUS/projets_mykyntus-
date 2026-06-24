using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Ai;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>API dédiée UI « Génération de documents » (IA directe, sans moteur local).</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class GenerateDocumentAiController(
    IMediator mediator,
    IDocumentationRequestContext userContext,
    IAiDirectDocumentAppService? aiDirect) : ControllerBase
{
    [HttpPost("generate-document-ai")]
    public async Task<IActionResult> Generate([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (aiDirect is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureRhOrAdmin(out var err)) return err;
        try
        {
            return Ok(await mediator.Send(new GenerateDocumentAiCommand(body), ct));
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost("generate-document-ai/preview")]
    public async Task<IActionResult> PreviewPdf([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (aiDirect is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureRhOrAdmin(out var err)) return err;
        try
        {
            var file = await mediator.Send(new PreviewDocumentAiPdfCommand(body), ct);
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("generate-document-ai/export")]
    public async Task<IActionResult> Export([FromBody] AiDirectRenderRequest body)
    {
        if (aiDirect is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureRhOrAdmin(out var err)) return err;
        try
        {
            var file = await mediator.Send(new ExportDocumentAiCommand(body));
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    private bool EnsureRhOrAdmin(out IActionResult error)
    {
        if (!userContext.UserId.HasValue)
        {
            error = Unauthorized();
            return false;
        }
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
        {
            error = Forbid();
            return false;
        }
        error = null!;
        return true;
    }
}
