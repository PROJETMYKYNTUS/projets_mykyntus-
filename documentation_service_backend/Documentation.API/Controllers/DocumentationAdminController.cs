using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Admin;
using Documentation.Application.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Administration DMS — persistance PostgreSQL (schéma documentation).</summary>
[ApiController]
[Authorize]
[Route("api/documentation")]
public sealed class DocumentationAdminController(
    IMediator mediator,
    IDocumentationDmsAdminAppService? admin) : ControllerBase
{
    [HttpGet("general-config")]
    public async Task<ActionResult<AdminGeneralConfigDto>> GetGeneralConfig(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new GetDmsGeneralConfigQuery(), ct);
        if (result is null)
            return NotFound(new { message = "Aucune configuration générale en base. Exécutez les scripts SQL (001 + 009)." });
        return Ok(result);
    }

    [HttpPut("general-config")]
    public async Task<ActionResult<AdminGeneralConfigDto>> SaveGeneralConfig([FromBody] AdminGeneralConfigDto body, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new SaveDmsGeneralConfigCommand(body), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("general-config/reset")]
    public Task<ActionResult<AdminGeneralConfigDto>> ResetGeneralConfig(CancellationToken ct) =>
        GetGeneralConfig(ct);

    [HttpGet("doc-types")]
    public async Task<ActionResult<List<AdminDocTypeDto>>> GetDocTypes(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetAdminDocTypesQuery(), ct));
    }

    [HttpPost("doc-types")]
    public async Task<ActionResult<AdminDocTypeDto>> CreateDocType([FromBody] CreateDocTypeRequestDto payload, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new CreateAdminDocTypeCommand(payload), ct));
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPut("doc-types/{id:guid}")]
    public async Task<ActionResult<AdminDocTypeDto>> UpdateDocType(Guid id, [FromBody] CreateDocTypeRequestDto payload, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(new UpdateAdminDocTypeCommand(id, payload), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpDelete("doc-types/{id:guid}")]
    public async Task<ActionResult<bool>> DeleteDocType(Guid id, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(new DeleteAdminDocTypeCommand(id), ct);
            return result is null ? NotFound(false) : Ok(result);
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("doc-types/reset")]
    public Task<ActionResult<List<AdminDocTypeDto>>> ResetDocTypes(CancellationToken ct) => GetDocTypes(ct);

    [HttpGet("workflow-definitions")]
    public async Task<ActionResult<List<AdminWorkflowDefinitionDto>>> GetWorkflowDefinitions(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetWorkflowDefinitionsQuery(), ct));
    }

    [HttpPut("workflow-definitions/{id:guid}")]
    public async Task<ActionResult<AdminWorkflowDefinitionDto>> UpdateWorkflowDefinition(
        Guid id,
        [FromBody] AdminWorkflowDefinitionDto body,
        CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(new UpdateWorkflowDefinitionCommand(id, body), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("workflow-definitions/reset")]
    public Task<ActionResult<List<AdminWorkflowDefinitionDto>>> ResetWorkflows(CancellationToken ct) =>
        GetWorkflowDefinitions(ct);

    [HttpGet("permission-policies")]
    public async Task<ActionResult<List<AdminPermissionPolicyDto>>> GetPermissionPolicies(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetPermissionPoliciesQuery(), ct));
    }

    [HttpPut("permission-policies")]
    public async Task<ActionResult<List<AdminPermissionPolicyDto>>> SavePermissionPolicies(
        [FromBody] List<AdminPermissionPolicyDto> body,
        CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new SavePermissionPoliciesCommand(body), ct));
        }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("permission-policies/reset")]
    public Task<ActionResult<List<AdminPermissionPolicyDto>>> ResetPermissionPolicies(CancellationToken ct) =>
        GetPermissionPolicies(ct);

    [HttpGet("storage-config")]
    public async Task<ActionResult<AdminStorageConfigDto>> GetStorageConfig(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new GetStorageConfigQuery(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("storage-config")]
    public async Task<ActionResult<AdminStorageConfigDto>> SaveStorageConfig([FromBody] AdminStorageConfigDto body, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new SaveStorageConfigCommand(body), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("storage-config/reset")]
    public Task<ActionResult<AdminStorageConfigDto>> ResetStorageConfig(CancellationToken ct) => GetStorageConfig(ct);

    [HttpGet("admin-roles")]
    public async Task<ActionResult<List<string>>> GetAdminRoles(CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { message = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetAdminRolesQuery(), ct));
    }
}
