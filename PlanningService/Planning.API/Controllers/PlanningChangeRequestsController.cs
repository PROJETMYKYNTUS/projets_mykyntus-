using Microsoft.AspNetCore.Mvc;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/planning/change-requests")]
public class PlanningChangeRequestsController(
    IPlanningChangeRequestService changeRequestService,
    IUserService userService) : ControllerBase
{
    private readonly IPlanningChangeRequestService _service = changeRequestService;
    private readonly IUserService _userService = userService;

    private async Task<int> ResolvePlanningUserIdAsync(int authUserId)
    {
        var user = await _userService.GetUserByAuthIdAsync(authUserId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        return user.Id;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromQuery] int authUserId,
        [FromBody] CreatePlanningChangeRequestDto dto)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            var result = await _service.CreateAsync(requesterId, dto);
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
            var result = await _service.GetMyAsync(requesterId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? weekCode)
    {
        var result = await _service.GetAllAsync(status, weekCode);
        return Ok(result);
    }

    [HttpGet("stats-by-employee")]
    public async Task<IActionResult> StatsByEmployee([FromQuery] string? weekCode)
    {
        var result = await _service.GetStatsByEmployeeAsync(weekCode);
        return Ok(result);
    }

    [HttpGet("swap-candidates")]
    public async Task<IActionResult> SwapCandidates(
        [FromQuery] int assignmentId,
        [FromQuery] int authUserId)
    {
        try
        {
            var requesterId = await ResolvePlanningUserIdAsync(authUserId);
            var result = await _service.GetSwapCandidatesAsync(assignmentId, requesterId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromQuery] int authUserId)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            var result = await _service.ApproveAsync(id, processedBy);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
        int id,
        [FromQuery] int authUserId,
        [FromBody] RejectPlanningChangeRequestDto? dto)
    {
        try
        {
            var processedBy = await ResolvePlanningUserIdAsync(authUserId);
            var result = await _service.RejectAsync(id, processedBy, dto?.Reason);
            return Ok(result);
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
            var result = await _service.CancelAsync(id, requesterId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
