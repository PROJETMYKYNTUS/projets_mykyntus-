using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Controllers;

/// <summary>API d'administration de la matrice RBAC (Phase 1.4).</summary>
[ApiController]
[Route("api/prime/admin/rbac")]
public sealed class RbacAdminController(PrimeDbContext? db) : ControllerBase
{
    private static RbacPermissionDto Map(RbacPermissionEntity e) => new()
    {
        Id = e.Id,
        Role = e.Role,
        Action = e.Action,
        Scope = e.Scope,
        IsAllowed = e.IsAllowed,
        UpdatedAt = e.UpdatedAt,
    };

    [HttpGet]
    public async Task<ActionResult<List<RbacPermissionDto>>> List(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var rows = await db.RbacPermissions.AsNoTracking().OrderBy(p => p.Role).ThenBy(p => p.Action).ThenBy(p => p.Scope).ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPut]
    public async Task<ActionResult<RbacPermissionDto>> Upsert([FromBody] UpsertRbacPermissionRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.Role) || string.IsNullOrWhiteSpace(body.Action) || string.IsNullOrWhiteSpace(body.Scope))
            return BadRequest(new { error = "Role, Action et Scope sont obligatoires." });

        var role = body.Role.Trim();
        var action = body.Action.Trim();
        var scope = body.Scope.Trim();

        var row = await db.RbacPermissions.FirstOrDefaultAsync(p => p.Role == role && p.Action == action && p.Scope == scope, ct);
        var now = DateTimeOffset.UtcNow;
        if (row == null)
        {
            row = new RbacPermissionEntity
            {
                Id = Guid.NewGuid(),
                Role = role,
                Action = action,
                Scope = scope,
                IsAllowed = body.IsAllowed,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.RbacPermissions.Add(row);
        }
        else
        {
            row.IsAllowed = body.IsAllowed;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.RbacPermissions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row == null) return NotFound();
        db.RbacPermissions.Remove(row);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
