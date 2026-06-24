using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Drafts;

namespace Prime.API.Controllers;

/// <summary>Aperçu fusionné fiche PRIME (validateurs W1, RH/Manager W2).</summary>
[ApiController]
[Route("api/prime/fiches")]
public sealed class PrimeFichePreviewController(
    IMediator mediator,
    IPrimeFichePreviewAppService? preview) : ControllerBase
{
    [HttpGet("{ficheId:guid}/merged-preview-context")]
    public async Task<ActionResult<MergedFichePreviewContextDto>> MergedPreviewContext(
        Guid ficheId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (preview is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetMergedFichePreviewContextQuery(ficheId, userId, role), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
