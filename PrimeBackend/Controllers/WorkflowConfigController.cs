using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>API de configuration du workflow de validation (Phase 1.4).</summary>
[ApiController]
[Route("api/prime/admin/workflow")]
public sealed class WorkflowConfigController(PrimeDbContext? db) : ControllerBase
{
    private static WorkflowStepConfigDto MapStep(WorkflowStepConfigEntity e) => new()
    {
        Id = e.Id,
        SortOrder = e.SortOrder,
        ApproverRole = e.ApproverRole,
        FromStatus = e.FromStatus,
        ToStatus = e.ToStatus,
        IsActive = e.IsActive,
        SlaHours = e.SlaHours,
        CapturesAmountsOnApproval = e.CapturesAmountsOnApproval,
        TerminalApproved = e.TerminalApproved,
        UpdatedAt = e.UpdatedAt,
    };

    private static WorkflowGlobalConfigDto MapGlobal(WorkflowGlobalConfigEntity e) => new()
    {
        Id = e.Id,
        NotificationsEnabled = e.NotificationsEnabled,
        GlobalSlaHours = e.GlobalSlaHours,
        AllowBulkApprove = e.AllowBulkApprove,
        RequireRejectReason = e.RequireRejectReason,
        UpdatedAt = e.UpdatedAt,
    };

    [HttpGet("steps")]
    public async Task<ActionResult<List<WorkflowStepConfigDto>>> ListSteps(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var rows = await db.WorkflowSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        return Ok(rows.Select(MapStep).ToList());
    }

    [HttpPost("steps")]
    public async Task<ActionResult<WorkflowStepConfigDto>> CreateStep([FromBody] UpsertWorkflowStepConfigRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.ApproverRole) || string.IsNullOrWhiteSpace(body.FromStatus) || string.IsNullOrWhiteSpace(body.ToStatus))
            return BadRequest(new { error = "ApproverRole, FromStatus et ToStatus sont obligatoires." });

        var now = DateTimeOffset.UtcNow;
        var row = new WorkflowStepConfigEntity
        {
            Id = Guid.NewGuid(),
            SortOrder = body.SortOrder,
            ApproverRole = body.ApproverRole.Trim(),
            FromStatus = body.FromStatus.Trim(),
            ToStatus = body.ToStatus.Trim(),
            IsActive = body.IsActive,
            SlaHours = body.SlaHours,
            CapturesAmountsOnApproval = body.CapturesAmountsOnApproval,
            TerminalApproved = body.TerminalApproved,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkflowSteps.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(MapStep(row));
    }

    [HttpPut("steps/{id:guid}")]
    public async Task<ActionResult<WorkflowStepConfigDto>> UpdateStep(Guid id, [FromBody] UpsertWorkflowStepConfigRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row == null) return NotFound();
        row.SortOrder = body.SortOrder;
        if (!string.IsNullOrWhiteSpace(body.ApproverRole)) row.ApproverRole = body.ApproverRole.Trim();
        if (!string.IsNullOrWhiteSpace(body.FromStatus)) row.FromStatus = body.FromStatus.Trim();
        if (!string.IsNullOrWhiteSpace(body.ToStatus)) row.ToStatus = body.ToStatus.Trim();
        row.IsActive = body.IsActive;
        row.SlaHours = body.SlaHours;
        row.CapturesAmountsOnApproval = body.CapturesAmountsOnApproval;
        row.TerminalApproved = body.TerminalApproved;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(MapStep(row));
    }

    /// <summary>Recalcule la chaîne Pending → … pour toutes les étapes actives (après réordonnancement).</summary>
    [HttpPost("steps/rechain")]
    public async Task<ActionResult<List<WorkflowStepConfigDto>>> RechainAllSteps(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var all = await db.WorkflowSteps.ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        WorkflowStepConfigRechain.ApplyToActiveSteps(all);
        foreach (var s in all)
            s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        var rows = all.OrderBy(s => s.SortOrder).ToList();
        return Ok(rows.Select(MapStep).ToList());
    }

    [HttpDelete("steps/{id:guid}")]
    public async Task<IActionResult> DeleteStep(Guid id, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row == null) return NotFound();
        db.WorkflowSteps.Remove(row);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("global")]
    public async Task<ActionResult<WorkflowGlobalConfigDto>> GetGlobal(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.WorkflowGlobalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (row == null)
        {
            row = new WorkflowGlobalConfigEntity { Id = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow };
            db.WorkflowGlobalConfigs.Add(row);
            await db.SaveChangesAsync(ct);
        }
        return Ok(MapGlobal(row));
    }

    [HttpPut("global")]
    public async Task<ActionResult<WorkflowGlobalConfigDto>> UpdateGlobal([FromBody] UpdateWorkflowGlobalConfigRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var row = await db.WorkflowGlobalConfigs.FirstOrDefaultAsync(ct);
        if (row == null)
        {
            row = new WorkflowGlobalConfigEntity { Id = Guid.NewGuid() };
            db.WorkflowGlobalConfigs.Add(row);
        }
        row.NotificationsEnabled = body.NotificationsEnabled;
        row.GlobalSlaHours = body.GlobalSlaHours;
        row.AllowBulkApprove = body.AllowBulkApprove;
        row.RequireRejectReason = body.RequireRejectReason;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(MapGlobal(row));
    }
}
