using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class WorkflowConfigAdminService(PrimeDbContext db) : IWorkflowConfigAdminService
{
    private static WorkflowStepConfigDto MapStep(WorkflowStepConfig e) => new()
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

    private static WorkflowGlobalConfigDto MapGlobal(WorkflowGlobalConfig e) => new()
    {
        Id = e.Id,
        NotificationsEnabled = e.NotificationsEnabled,
        GlobalSlaHours = e.GlobalSlaHours,
        AllowBulkApprove = e.AllowBulkApprove,
        RequireRejectReason = e.RequireRejectReason,
        UpdatedAt = e.UpdatedAt,
    };

    public async Task<IReadOnlyList<WorkflowStepConfigDto>> ListStepsAsync(CancellationToken ct = default)
    {
        var rows = await db.WorkflowSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        return rows.Select(MapStep).ToList();
    }

    public async Task<WorkflowStepConfigDto> CreateStepAsync(UpsertWorkflowStepConfigRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.ApproverRole) || string.IsNullOrWhiteSpace(body.FromStatus) || string.IsNullOrWhiteSpace(body.ToStatus))
            throw new ArgumentException("ApproverRole, FromStatus et ToStatus sont obligatoires.");

        var now = DateTimeOffset.UtcNow;
        var row = new WorkflowStepConfig
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
        return MapStep(row);
    }

    public async Task<WorkflowStepConfigDto?> UpdateStepAsync(Guid id, UpsertWorkflowStepConfigRequest body, CancellationToken ct = default)
    {
        var row = await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null)
            return null;

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
        return MapStep(row);
    }

    public async Task<IReadOnlyList<WorkflowStepConfigDto>> RechainAllStepsAsync(CancellationToken ct = default)
    {
        var all = await db.WorkflowSteps.ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        WorkflowStepConfigRechain.ApplyToActiveSteps(all);
        foreach (var s in all)
            s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return all.OrderBy(s => s.SortOrder).Select(MapStep).ToList();
    }

    public async Task<bool> DeleteStepAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null)
            return false;
        db.WorkflowSteps.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WorkflowGlobalConfigDto> GetGlobalAsync(CancellationToken ct = default)
    {
        var row = await db.WorkflowGlobalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new WorkflowGlobalConfig { Id = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow };
            db.WorkflowGlobalConfigs.Add(row);
            await db.SaveChangesAsync(ct);
        }
        return MapGlobal(row);
    }

    public async Task<WorkflowGlobalConfigDto> UpdateGlobalAsync(UpdateWorkflowGlobalConfigRequest body, CancellationToken ct = default)
    {
        var row = await db.WorkflowGlobalConfigs.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new WorkflowGlobalConfig { Id = Guid.NewGuid() };
            db.WorkflowGlobalConfigs.Add(row);
        }
        row.NotificationsEnabled = body.NotificationsEnabled;
        row.GlobalSlaHours = body.GlobalSlaHours;
        row.AllowBulkApprove = body.AllowBulkApprove;
        row.RequireRejectReason = body.RequireRejectReason;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapGlobal(row);
    }
}
