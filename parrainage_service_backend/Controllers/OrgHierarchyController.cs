using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/org")]
public sealed class OrgHierarchyController(ParrainageDbContext db) : ControllerBase
{
    public sealed record OrgNodeDto(string Id, string? ParentId, string Email, string Role, string Name);

    [HttpGet("nodes")]
    public async Task<ActionResult<List<OrgNodeDto>>> GetNodes(CancellationToken ct)
    {
        var rows = await db.PortalUsers.AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new OrgNodeDto(u.Id, u.ParentId, u.Email, u.Role, u.Name))
            .ToListAsync(ct);
        return Ok(rows);
    }
}
