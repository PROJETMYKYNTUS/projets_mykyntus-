using Kyntus.Iam;
using Kyntus.Identity.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Application.Users;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(
    IMediator mediator,
    IUserService userService,
    IPolicyEvaluator policyEvaluator) : ControllerBase
{
    private async Task<ActionResult?> DenyUnlessCanManageUsersAsync()
    {
        var role = User.GetAuthRole() ?? "Employee";
        if (IsHrOrAdmin(role))
            return null;

        var subjectId = User.GetSubjectId() ?? Guid.Empty;
        var decision = await policyEvaluator.EvaluateAsync(
            new PolicyRequest(subjectId, role, "users:manage", "users", null));
        if (!decision.Allowed)
            return Forbid();
        return null;
    }

    private static bool IsHrOrAdmin(string role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "RH", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("by-subservice/{subServiceId}")]
    public async Task<ActionResult<List<UserDto>>> GetBySubService(int subServiceId)
    {
        var users = await userService.GetUsersBySubServiceAsync(subServiceId);
        return Ok(users);
    }

    [HttpGet("managers-by-subservice/{subServiceId}")]
    public async Task<ActionResult<List<UserDto>>> GetManagersBySubService(int subServiceId, CancellationToken ct)
    {
        var users = await mediator.Send(new GetManagersBySubServiceQuery(subServiceId), ct);
        return Ok(users);
    }

    [HttpPost("sync-to-conge")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<IActionResult> SyncAllToConge()
    {
        await userService.SyncAllEmployesToCongeAsync();
        return Ok(new { message = "Synchronisation envoyée via RabbitMQ." });
    }

    [HttpPut("{id}/new-employee")]
    public async Task<IActionResult> SetNewEmployeeStatus(int id, [FromBody] SetNewEmployeeDto dto, CancellationToken ct)
    {
        var result = await mediator.Send(new SetNewEmployeeStatusCommand(id, dto), ct);
        if (result is null)
            return NotFound(new { message = "Employé introuvable." });
        return Ok(result);
    }

    [HttpGet("by-auth/{authUserId:int}")]
    public async Task<ActionResult<UserDto>> GetByAuthId(int authUserId)
    {
        var user = await userService.GetUserByAuthIdAsync(authUserId);
        if (user == null)
            return NotFound(new { message = "Utilisateur introuvable." });
        return Ok(user);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(new { message = $"Utilisateur {id} introuvable." });
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
    {
        if (await DenyUnlessCanManageUsersAsync() is { } denied) return denied;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var user = await userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserDto dto)
    {
        if (await DenyUnlessCanManageUsersAsync() is { } denied) return denied;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var user = await userService.UpdateUserAsync(id, dto);
            if (user == null)
                return NotFound(new { message = $"Utilisateur {id} introuvable." });
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await DenyUnlessCanManageUsersAsync() is { } denied) return denied;
        var result = await userService.DeleteUserAsync(id);
        if (!result)
            return NotFound(new { message = $"Utilisateur {id} introuvable." });
        return NoContent();
    }

    [HttpGet("check-email/{email}")]
    public async Task<ActionResult<bool>> CheckEmail(string email, [FromQuery] int? excludeId = null)
    {
        var isUnique = await userService.IsEmailUniqueAsync(email, excludeId);
        return Ok(new { isUnique });
    }
}
