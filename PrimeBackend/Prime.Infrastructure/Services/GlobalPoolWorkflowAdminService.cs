using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class GlobalPoolWorkflowAdminService(PrimeDbContext db) : IGlobalPoolWorkflowAdminService
{
    private static GlobalPoolWorkflowStepDto Map(GlobalPoolWorkflowStep e) => new()
    {
        Id = e.Id,
        SortOrder = e.SortOrder,
        ApproverRole = e.ApproverRole,
        IsRequired = e.IsRequired,
        IsActive = e.IsActive,
    };

    public async Task<IReadOnlyList<GlobalPoolWorkflowStepDto>> ListStepsAsync(CancellationToken ct = default)
    {
        var rows = await db.GlobalPoolWorkflowSteps.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ApproverRole)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<GlobalPoolWorkflowStepDto> CreateStepAsync(UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.ApproverRole))
            throw new ArgumentException("ApproverRole est obligatoire.");

        var now = DateTimeOffset.UtcNow;
        var row = new GlobalPoolWorkflowStep
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
        return Map(row);
    }

    public async Task<GlobalPoolWorkflowStepDto?> UpdateStepAsync(Guid id, UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct = default)
    {
        var row = await db.GlobalPoolWorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null)
            return null;

        row.SortOrder = body.SortOrder;
        if (!string.IsNullOrWhiteSpace(body.ApproverRole)) row.ApproverRole = body.ApproverRole.Trim();
        row.IsRequired = body.IsRequired;
        row.IsActive = body.IsActive;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<bool> DeleteStepAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.GlobalPoolWorkflowSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null)
            return false;
        db.GlobalPoolWorkflowSteps.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
