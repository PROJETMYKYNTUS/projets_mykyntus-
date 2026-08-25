using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Drafts;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/supervisor-campaign")]
public sealed class SupervisorCampaignController(
    IMediator mediator,
    ISupervisorCampaignAppService? campaign) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SupervisorCelluleCampaignDto>>> Get(
        [FromQuery] string supervisorUserId,
        [FromQuery] string period,
        CancellationToken ct)
    {
        if (campaign is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetSupervisorCampaignQuery(supervisorUserId, period), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
