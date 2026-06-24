using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.Directory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Annuaire et organisation (projection directory) — données PostgreSQL du tenant courant.</summary>
[ApiController]
[Route("api/documentation/data")]
public sealed class DocumentationDirectoryController(
    IMediator mediator,
    IDocumentationRequestContext userContext,
    IDirectoryQueryAppService? directory) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetDirectoryUsers(CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListDirectoryUsersQuery(), ct));
    }

    [HttpGet("users/me")]
    public async Task<ActionResult<DirectoryUserResponse>> GetDirectoryUserMe(CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        var result = await mediator.Send(new GetDirectoryUserQuery(userContext.UserId.Value), ct);
        if (result is null)
            return NotFound(new { message = "Utilisateur absent de l’annuaire pour ce tenant." });
        return Ok(result);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<DirectoryUserResponse>> GetDirectoryUser(Guid id, CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new GetDirectoryUserQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("organisation/poles")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetPoles(CancellationToken ct) =>
        GetPolesCore(ct);

    [HttpGet("organization/poles")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetPolesOrganizationSpelling(CancellationToken ct) =>
        GetPolesCore(ct);

    [HttpGet("organisation/cellules")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetCellulesByPole([FromQuery] Guid poleId, CancellationToken ct) =>
        GetCellulesCore(poleId, ct);

    [HttpGet("organization/cellules")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetCellulesByPoleOrganizationSpelling(
        [FromQuery] Guid poleId,
        CancellationToken ct) =>
        GetCellulesCore(poleId, ct);

    [HttpGet("organisation/departements")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetDepartementsByCellule(
        [FromQuery] Guid celluleId,
        CancellationToken ct) =>
        GetDepartementsCore(celluleId, ct);

    [HttpGet("organization/departements")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetDepartementsByCelluleOrganizationSpelling(
        [FromQuery] Guid celluleId,
        CancellationToken ct) =>
        GetDepartementsCore(celluleId, ct);

    [HttpGet("users/by-role-org")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetUsersByRoleAndOrg(
        [FromQuery] string role,
        [FromQuery] Guid poleId,
        [FromQuery] Guid celluleId,
        [FromQuery] Guid departementId,
        CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetUsersByRoleAndOrgQuery(role, poleId, celluleId, departementId), ct));
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpGet("users/managers")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetManagersByDepartement(
        [FromQuery] Guid departementId,
        CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetManagersByDepartementQuery(departementId), ct));
    }

    [HttpGet("users/coaches")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetCoachsByManager(
        [FromQuery] Guid managerId,
        [FromQuery] Guid? departementId,
        CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetCoachesByManagerQuery(managerId, departementId), ct));
    }

    [HttpGet("users/pilotes")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetPilotesByCoach(
        [FromQuery] Guid coachId,
        [FromQuery] Guid? departementId,
        CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetPilotesByCoachQuery(coachId, departementId), ct));
    }

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetPolesCore(CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetOrganisationPolesQuery(), ct));
    }

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetCellulesCore(Guid poleId, CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetOrganisationCellulesQuery(poleId), ct));
    }

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetDepartementsCore(Guid celluleId, CancellationToken ct)
    {
        if (directory is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetOrganisationDepartementsQuery(celluleId), ct));
    }
}
