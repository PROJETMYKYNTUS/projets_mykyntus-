// PlanningService/Controllers/RolesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RolesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await _context.Roles
            .Where(r => r.IsActive)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        return Ok(roles);
    }
}