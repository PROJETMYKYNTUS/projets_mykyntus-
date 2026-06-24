using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class AnomalyAdminService(PrimeDbContext db, AnomalyDetectionService detection) : IAnomalyAdminService
{
    private static readonly string[] AllowedStatuses = ["Open", "InReview", "Resolved", "Ignored"];

    private static AnomalyDto Map(Anomaly e) => new()
    {
        Id = e.Id,
        DetectedAt = e.DetectedAt,
        UpdatedAt = e.UpdatedAt,
        Type = e.Type,
        Severity = e.Severity,
        Status = e.Status,
        Description = e.Description,
        TargetEntityType = e.TargetEntityType,
        TargetEntityId = e.TargetEntityId,
        Period = e.Period,
        ServiceId = e.ServiceId,
        CelluleId = e.CelluleId,
        PoleId = e.PoleId,
        ContextJson = e.ContextJson,
        ResolvedByUserId = e.ResolvedByUserId,
        ResolvedAt = e.ResolvedAt,
        ResolutionNote = e.ResolutionNote,
    };

    public async Task<IReadOnlyList<AnomalyDto>> ListAsync(AnomalyListFilter filter, CancellationToken ct = default)
    {
        var q = db.Anomalies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(a => a.Status == filter.Status.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Type)) q = q.Where(a => a.Type == filter.Type.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Severity)) q = q.Where(a => a.Severity == filter.Severity.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Period)) q = q.Where(a => a.Period == filter.Period.Trim());
        if (!string.IsNullOrWhiteSpace(filter.ServiceId)) q = q.Where(a => a.ServiceId == filter.ServiceId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.CelluleId)) q = q.Where(a => a.CelluleId == filter.CelluleId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.PoleId)) q = q.Where(a => a.PoleId == filter.PoleId.Trim());
        var rows = await q.OrderByDescending(a => a.DetectedAt).Take(500).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<AnomalyDto?> UpdateStatusAsync(Guid id, UpdateAnomalyStatusBody body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.Status))
            throw new ArgumentException("Status est obligatoire.");
        if (!AllowedStatuses.Contains(body.Status))
            throw new ArgumentException("Status invalide.");

        var row = await db.Anomalies.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        row.Status = body.Status;
        row.UpdatedAt = now;
        if (body.Status is "Resolved" or "Ignored")
        {
            row.ResolvedAt = now;
            row.ResolvedByUserId = body.ResolvedByUserId;
            row.ResolutionNote = body.ResolutionNote;
        }
        else
        {
            row.ResolvedAt = null;
            row.ResolvedByUserId = null;
            row.ResolutionNote = null;
        }
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public Task<int> RecomputeAllAsync(CancellationToken ct = default) =>
        detection.RecomputeAllAsync(ct);
}
