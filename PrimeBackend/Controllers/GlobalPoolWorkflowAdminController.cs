using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/admin/global-pool-workflow")]
public sealed class GlobalPoolWorkflowAdminController(PrimeDbContext? db) : ControllerBase
{
    private static GlobalPoolWorkflowStepDto Map(GlobalPoolWorkflowStepEntity e) => new()
    {
        Id = e.Id,
        SortOrder = e.SortOrder,
        ApproverRole = e.ApproverRole,
        IsRequired = e.IsRequired,
        IsActive = e.IsActive,
    };

    [HttpGet("steps")]
    public async Task<ActionResult<List<GlobalPoolWorkflowStepDto>>> List(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var rows = await db.GlobalPoolWorkflowSteps.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ApproverRole)
            .ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost("steps")]
    public async Task<ActionResult<GlobalPoolWorkflowStepDto>> Create([FromBody] UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.ApproverRole))
            return BadRequest(new { error = "ApproverRole est obligatoire." });
        var now = DateTimeOffset.UtcNow;
        var row = new GlobalPoolWorkflowStepEntity
        {
            Id = Guid.NewGuid(),
            SortOrder = body.SortOrder,
            ApproverRole = body.ApproverRole.Trim(),
            IsRequired = body.IsRequired,
            IsActive = body.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GlobalPoolWorkflowSteps.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    [HttpPut("steps/{id:guid}")]
    public async Task<ActionResult<GlobalPoolWorkflowStepDto>> Update(Guid id, [FromBody] UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.GlobalPoolWorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row == null) return NotFound();
        row.SortOrder = body.SortOrder;
        if (!string.IsNullOrWhiteSpace(body.ApproverRole)) row.ApproverRole = body.ApproverRole.Trim();
        row.IsRequired = body.IsRequired;
        row.IsActive = body.IsActive;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    [HttpDelete("steps/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.GlobalPoolWorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row == null) return NotFound();
        db.GlobalPoolWorkflowSteps.Remove(row);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
