using Microsoft.AspNetCore.Mvc;
using Kyntus.Identity.Jwt;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/planning/exceptional-requests")]
public class PlanningExceptionalRequestsController(
    IPlanningExceptionalRequestService exceptionalRequestService,
    IUserService userService) : ControllerBase
{
    private readonly IPlanningExceptionalRequestService _service = exceptionalRequestService;
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
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [FromQuery] int authUserId,
        [FromForm] DateOnly requestedDate,
        [FromForm] int requestedShiftTemplateId,
        [FromForm] string reason,
        IFormFile? file)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            Stream? stream = null;
            if (file is { Length: > 0 })
                stream = file.OpenReadStream();

            await using var _ = stream;
            var result = await _service.CreateAsync(
                requesterId,
                requestedDate,
                requestedShiftTemplateId,
                reason ?? string.Empty,
                stream,
                file?.FileName,
                file?.ContentType);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy([FromQuery] int authUserId)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.GetMyAsync(requesterId));
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
        [FromQuery] int? authUserId = null,
        [FromQuery] int? requesterUserId = null)
    {
        int? viewerId = null;
        if (authUserId is > 0)
        {
            try
            {
                viewerId = await ResolvePlanningUserIdAsync(authUserId.Value);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        return Ok(await _service.GetAllAsync(status, weekCode, viewerId, requesterUserId));
    }

    [HttpGet("quota")]
    public async Task<IActionResult> Quota([FromQuery] int authUserId, [FromQuery] int? year = null)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.GetQuotaAsync(requesterId, year));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("available-shifts")]
    public async Task<IActionResult> AvailableShifts([FromQuery] int authUserId)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.GetAvailableShiftsAsync(requesterId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("target-week")]
    public async Task<IActionResult> TargetWeek()
    {
        return Ok(await _service.GetTargetWeekAsync());
    }

    [HttpPost("{id:int}/supervisor-approve")]
    public async Task<IActionResult> SupervisorApprove(int id, [FromQuery] int authUserId)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.SupervisorApproveAsync(id, processedBy));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/supervisor-reject")]
    public async Task<IActionResult> SupervisorReject(
        int id,
        [FromQuery] int authUserId,
        [FromBody] RejectPlanningExceptionalRequestDto? dto)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.SupervisorRejectAsync(id, processedBy, dto?.Reason));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/rh-approve")]
    public async Task<IActionResult> RhApprove(int id, [FromQuery] int authUserId)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.RhApproveAsync(id, processedBy));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/rh-reject")]
    public async Task<IActionResult> RhReject(
        int id,
        [FromQuery] int authUserId,
        [FromBody] RejectPlanningExceptionalRequestDto? dto)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.RhRejectAsync(id, processedBy, dto?.Reason));
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
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            return Ok(await _service.CancelAsync(id, requesterId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/justification")]
    public async Task<IActionResult> DownloadJustification(int id, [FromQuery] int authUserId)
    {
        try
        {
            var viewerId = await ResolvePlanningUserIdAsync(authUserId);
            var file = await _service.GetJustificationAsync(id, viewerId);
            if (file == null)
                return NotFound(new { message = "Aucun justificatif." });
            return File(file.Value.Content, file.Value.ContentType, file.Value.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
