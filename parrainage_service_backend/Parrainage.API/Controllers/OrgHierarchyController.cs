using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Abstractions;
using Parrainage.Application.Org;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/org")]
public sealed class OrgHierarchyController(IMediator mediator) : ControllerBase
{
    [HttpGet("nodes")]
    public async Task<ActionResult<List<OrgNodeDto>>> GetNodes(CancellationToken ct) =>
        Ok(await mediator.Send(new ListOrgNodesQuery(), ct));
}
