using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AppDbContext _context;

    public UsersController(IUserService userService, AppDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("by-subservice/{subServiceId}")]
    public async Task<ActionResult<List<UserDto>>> GetBySubService(int subServiceId)
    {
        var users = await _userService.GetUsersBySubServiceAsync(subServiceId);
        return Ok(users);
    }

    // Managers et Coachs d'un sous-service
    [HttpGet("managers-by-subservice/{subServiceId}")]
    public async Task<ActionResult<List<UserDto>>> GetManagersBySubService(int subServiceId)
    {
        var users = await _context.UserSubServices
            .Include(us => us.User).ThenInclude(u => u.Role)
            .Include(us => us.User).ThenInclude(u => u.SubService)
            .Include(us => us.User)
                .ThenInclude(u => u.ManagedSubServices)
                    .ThenInclude(ms => ms.SubService)
                        .ThenInclude(s => s.Service)
            .Where(us => us.SubServiceId == subServiceId)
            .Select(us => us.User)
            .Distinct()
            .ToListAsync();

        var result = users.Select(u => new UserDto
        {
            Id = u.Id,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name ?? string.Empty,
            SubServiceId = u.SubServiceId,
            SubServiceName = u.SubService?.Name,
            ManagedSubServices = u.ManagedSubServices?.Select(ms => new SubServiceSimpleDto
            {
                Id = ms.SubService.Id,
                Name = ms.SubService.Name,
                ServiceName = ms.SubService.Service?.Name ?? string.Empty
            }).ToList() ?? new(),
            FirstName = u.FirstName,
            LastName = u.LastName,
            HireDate = u.HireDate,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Ok(result);
    }
    [HttpPut("{id}/new-employee")]
    public async Task<IActionResult> SetNewEmployeeStatus(
    int id, [FromBody] SetNewEmployeeDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Employé introuvable." });

        user.IsNewEmployee = dto.IsNewEmployee;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            userId = user.Id,
            fullName = $"{user.FirstName} {user.LastName}",
            isNewEmployee = user.IsNewEmployee,
            hireDate = user.HireDate
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(new { message = $"Utilisateur {id} introuvable." });
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var user = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var user = await _userService.UpdateUserAsync(id, dto);
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
        var result = await _userService.DeleteUserAsync(id);
        if (!result)
            return NotFound(new { message = $"Utilisateur {id} introuvable." });
        return NoContent();
    }

    [HttpGet("check-email/{email}")]
    public async Task<ActionResult<bool>> CheckEmail(string email, [FromQuery] int? excludeId = null)
    {
        var isUnique = await _userService.IsEmailUniqueAsync(email, excludeId);
        return Ok(new { isUnique });
    }
}