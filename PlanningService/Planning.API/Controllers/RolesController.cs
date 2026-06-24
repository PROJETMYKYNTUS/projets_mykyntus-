using MediatR;
using Microsoft.AspNetCore.Mvc;
using Planning.Application.Queries.Roles;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllRoles(CancellationToken ct)
    {
        var roles = await mediator.Send(new GetAllRolesQuery(), ct);
        return Ok(roles);
    }
}
