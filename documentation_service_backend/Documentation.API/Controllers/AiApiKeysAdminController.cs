using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Admin;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Gestion des clés API IA par tenant (admin uniquement).</summary>
[ApiController]
[Authorize]
[Route("api/documentation/data/admin/ai-api-keys")]
public sealed class AiApiKeysAdminController(
    IMediator mediator,
    IDocumentationRequestContext userContext,
    IAiApiKeyAdminAppService? admin) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiApiKeyListItemResponse>>> List(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureAdmin(out var forbidden)) return forbidden;
        return Ok(await mediator.Send(new ListAiApiKeysQuery(), ct));
    }

    [HttpPost]
    public async Task<ActionResult<AiApiKeyListItemResponse>> Create([FromBody] CreateAiApiKeyRequest body, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureAdmin(out var forbidden)) return forbidden;
        try
        {
            return Ok(await mediator.Send(new CreateAiApiKeyCommand(body), ct));
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureAdmin(out var forbidden)) return forbidden;
        try
        {
            await mediator.Send(new ActivateAiApiKeyCommand(id), ct);
            return NoContent();
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 404) { return NotFound(); }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureAdmin(out var forbidden)) return forbidden;
        try
        {
            await mediator.Send(new DeactivateAiApiKeyCommand(id), ct);
            return NoContent();
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 404) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!EnsureAdmin(out var forbidden)) return forbidden;
        try
        {
            await mediator.Send(new DeleteAiApiKeyCommand(id), ct);
            return NoContent();
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 404) { return NotFound(); }
    }

    private bool EnsureAdmin(out ActionResult forbidden)
    {
        if (!userContext.UserId.HasValue || userContext.Role != AppRole.Admin)
        {
            forbidden = Forbid();
            return false;
        }

        forbidden = null!;
        return true;
    }
}
