using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.Workflow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

[ApiController]
[Authorize]
[Route("api/documentation/data/workflow")]
public sealed class DocumentationWorkflowController(
    IMediator mediator,
    IDocumentationWorkflowAppService? workflow) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<ActionResult<DocumentRequestResponse>> Validate(
        [FromBody] WorkflowValidateBody body,
        CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new ValidateDocumentWorkflowCommand(body), ct);
        return Map(result);
    }

    [HttpPost("approve")]
    public async Task<ActionResult<DocumentRequestResponse>> Approve(
        [FromBody] WorkflowApproveBody body,
        CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new ApproveDocumentWorkflowCommand(body), ct);
        return Map(result);
    }

    [HttpPost("reject")]
    public async Task<ActionResult<DocumentRequestResponse>> Reject(
        [FromBody] WorkflowRejectBody body,
        CancellationToken ct)
    {
        if (workflow is null) return StatusCode(503, new { message = "Base de données non configurée." });
        var result = await mediator.Send(new RejectDocumentWorkflowCommand(body), ct);
        return Map(result);
    }

    private ActionResult<DocumentRequestResponse> Map(WorkflowOperationResult result) =>
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
