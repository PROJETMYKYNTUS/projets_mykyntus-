using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.DocumentRequests;
using Documentation.Application.Workflow;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

/// <summary>Demandes de documents — CRUD, champs formulaire et alias workflow REST.</summary>
[ApiController]
[Route("api/documentation/data")]
public sealed class DocumentationDocumentRequestsController(
    IMediator mediator,
    IDocumentationTenantAccessor tenantAccessor,
    IDocumentRequestAppService? requests,
    IDocumentationWorkflowAppService? workflow,
    ILogger<DocumentationDocumentRequestsController> logger) : ControllerBase
{
    [HttpGet("document-requests")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetDocumentRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default) =>
        await ListAsync(new DocumentRequestListQuery
        {
            Scope = DocumentRequestListScope.AllVisible,
            Page = page,
            PageSize = pageSize,
            Status = status,
            Type = type,
            Role = role,
            SortBy = sortBy,
            SortOrder = sortOrder,
        }, ct);

    [HttpGet("document-requests/my-requests")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetMyDocumentRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default) =>
        await ListAsync(new DocumentRequestListQuery
        {
            Scope = DocumentRequestListScope.MyRequests,
            Page = page,
            PageSize = pageSize,
            Status = status,
            Type = type,
            SortBy = sortBy,
            SortOrder = sortOrder,
        }, ct);

    [HttpGet("document-requests/assigned-to-me")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetDocumentRequestsAssignedToMe(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default) =>
        await ListAsync(new DocumentRequestListQuery
        {
            Scope = DocumentRequestListScope.AssignedToMe,
            Page = page,
            PageSize = pageSize,
            Status = status,
            Type = type,
            SortBy = sortBy,
            SortOrder = sortOrder,
        }, ct);

    [HttpGet("document-requests/{id:guid}")]
    public async Task<ActionResult<DocumentRequestResponse>> GetDocumentRequest(Guid id, CancellationToken ct)
    {
        if (requests is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new GetDocumentRequestQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("document-requests/{id:guid}/field-values")]
    public async Task<ActionResult<DocumentRequestFieldValuesResponse>> GetDocumentRequestFieldValues(Guid id, CancellationToken ct)
    {
        if (requests is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new GetDocumentRequestFieldValuesQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("document-requests/{id:guid}/field-values")]
    public async Task<ActionResult<DocumentRequestFieldValuesResponse>> PutDocumentRequestFieldValues(
        Guid id,
        [FromBody] PutDocumentRequestFieldValuesRequest body,
        CancellationToken ct)
    {
        if (requests is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new PutDocumentRequestFieldValuesCommand(id, body), ct));
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 404) { return NotFound(); }
        catch (DocumentationApiException ex) when (ex.StatusCode == 403) { return Forbid(); }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("document-requests")]
    public async Task<ActionResult<DocumentRequestResponse>> CreateDocumentRequest(
        [FromBody] CreateDocumentRequestBody body,
        CancellationToken ct)
    {
        if (requests is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new CreateDocumentRequestCommand(body), ct));
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 401) { return Unauthorized(); }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPut("document-requests/{id:guid}/validate")]
    public async Task<ActionResult<DocumentRequestResponse>> PutValidateDocumentRequest(
        Guid id,
        [FromBody] WorkflowValidatePutBody? body,
        CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        logger.LogInformation("PutValidateDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var result = await mediator.Send(new ValidateDocumentWorkflowCommand(new WorkflowValidateBody
        {
            DocumentRequestId = id,
            Comment = body?.Comment,
        }), ct);
        return MapWorkflowResult(result);
    }

    [HttpPut("document-requests/{id:guid}/approve")]
    public async Task<ActionResult<DocumentRequestResponse>> PutApproveDocumentRequest(Guid id, CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        logger.LogInformation("PutApproveDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var result = await mediator.Send(new ApproveDocumentWorkflowCommand(new WorkflowApproveBody { DocumentRequestId = id }), ct);
        return MapWorkflowResult(result);
    }

    [HttpPut("document-requests/{id:guid}/reject")]
    public async Task<ActionResult<DocumentRequestResponse>> PutRejectDocumentRequest(
        Guid id,
        [FromBody] WorkflowRejectPutBody body,
        CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        logger.LogInformation("PutRejectDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var result = await mediator.Send(new RejectDocumentWorkflowCommand(new WorkflowRejectBody
        {
            DocumentRequestId = id,
            RejectionReason = body.RejectionReason ?? "",
        }), ct);
        return MapWorkflowResult(result);
    }

    private async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> ListAsync(
        DocumentRequestListQuery query,
        CancellationToken ct)
    {
        if (requests is null) return StatusCode(503, new { message = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ListDocumentRequestsQuery(query), ct));
        }
        catch (DocumentationApiException ex) when (ex.StatusCode == 401) { return Unauthorized(); }
        catch (DocumentationApiException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    private ActionResult<DocumentRequestResponse> MapWorkflowResult(WorkflowOperationResult result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Response!),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Error ?? "Demande introuvable." }),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Error }),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Error }),
            _ => StatusCode(result.StatusCode, new { message = result.Error }),
        };
}
