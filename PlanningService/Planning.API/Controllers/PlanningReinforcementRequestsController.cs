using Microsoft.AspNetCore.Mvc;
using Kyntus.Identity.Jwt;
using Planning.Application.Abstractions;
using Planning.Application.Common;
using Planning.Application.DTOs.Planning;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/planning/reinforcement-requests")]
public class PlanningReinforcementRequestsController(
    IPlanningReinforcementRequestService reinforcementService,
    IUserService userService) : ControllerBase
{
    private readonly IPlanningReinforcementRequestService _service = reinforcementService;
    private readonly IUserService _userService = userService;

    private async Task<int> ResolvePlanningUserIdAsync(int authUserId)
    {
        var user = await _userService.GetUserByAuthIdAsync(authUserId);
        if (user is not null)
            return user.Id;

        var ensured = await _userService.GetOrEnsureUserForAuthAsync(
            authUserId,
            User.GetEmail()?.Trim(),
            User.GetAuthRole(),
            User.GetSubjectId());
        if (ensured is not null)
            return ensured.Id;

        throw new InvalidOperationException("Utilisateur introuvable.");
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromQuery] int authUserId,
        [FromBody] CreatePlanningReinforcementRequestDto dto)
    {
        try
        {
            var creatorId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.CreateAsync(creatorId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? weekCode,
        [FromQuery] string? period = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int? authUserId = null)
    {
        int? viewerId = null;
        if (authUserId is > 0)
        {
            try { viewerId = await ResolvePlanningUserIdAsync(authUserId.Value); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        DateOnly? rangeFrom = null;
        DateOnly? rangeTo = null;
        if (string.IsNullOrWhiteSpace(weekCode)
            && (!string.IsNullOrWhiteSpace(period) || from.HasValue || to.HasValue))
            (rangeFrom, rangeTo) = PeriodRange.Resolve(period, from, to);

        return Ok(await _service.GetAllAsync(status, weekCode, viewerId, rangeFrom, rangeTo));
    }

    [HttpGet("contributor-stats")]
    public async Task<IActionResult> GetContributorStats(
        [FromQuery] string? period = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int? subServiceId = null,
        [FromQuery] int? authUserId = null)
    {
        int? viewerId = null;
        if (authUserId is > 0)
        {
            try { viewerId = await ResolvePlanningUserIdAsync(authUserId.Value); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        var (rangeFrom, rangeTo) = PeriodRange.Resolve(period, from, to);
        return Ok(await _service.GetContributorStatsAsync(
            viewerId, rangeFrom, rangeTo, subServiceId));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy([FromQuery] int authUserId)
    {
        try
        {
            var userId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.GetMyAsync(userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? authUserId = null)
    {
        int? viewerId = null;
        if (authUserId is > 0)
        {
            try { viewerId = await ResolvePlanningUserIdAsync(authUserId.Value); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        var dto = await _service.GetByIdAsync(id, viewerId);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:int}/volunteer-accept")]
    public async Task<IActionResult> VolunteerAccept(int id, [FromQuery] int authUserId)
    {
        try
        {
            var userId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.VolunteerAcceptAsync(id, userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/volunteer-decline")]
    public async Task<IActionResult> VolunteerDecline(int id, [FromQuery] int authUserId)
    {
        try
        {
            var userId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.VolunteerDeclineAsync(id, userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/select")]
    public async Task<IActionResult> Select(
        int id,
        [FromQuery] int authUserId,
        [FromBody] SelectReinforcementVolunteersDto dto)
    {
        try
        {
            var userId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.SelectAsync(id, userId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] int authUserId)
    {
        try
        {
            var userId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.CancelAsync(id, userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
