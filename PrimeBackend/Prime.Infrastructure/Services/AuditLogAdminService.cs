using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class AuditLogAdminService(PrimeDbContext db, PrimeAuditLogService auditWriter) : IAuditLogAdminService
{
    private static AuditLogDto Map(AuditLog e) => new()
    {
        Id = e.Id,
        At = e.At,
        UserId = e.UserId,
        UserDisplayName = e.UserDisplayName,
        Role = e.Role,
        Action = e.Action,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        DetailJson = e.DetailJson,
        IpAddress = e.IpAddress,
    };

    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogListFilter filter, CancellationToken ct = default)
    {
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.UserId)) q = q.Where(l => l.UserId == filter.UserId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Role)) q = q.Where(l => l.Role == filter.Role.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Action)) q = q.Where(l => l.Action == filter.Action.Trim());
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) q = q.Where(l => l.EntityType == filter.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) q = q.Where(l => l.EntityId == filter.EntityId.Trim());
        if (filter.From.HasValue) q = q.Where(l => l.At >= filter.From.Value);
        if (filter.To.HasValue) q = q.Where(l => l.At <= filter.To.Value);
        q = q.OrderByDescending(l => l.At);
        var max = Math.Clamp(filter.Take ?? 200, 1, 1000);
        var rows = await q.Take(max).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public Task RecordNavigationAsync(RecordAuditNavigationRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Route))
            throw new ArgumentException("userId et route sont requis.");
        return auditWriter.RecordNavigationAsync(body.UserId, body.UserDisplayName, body.Role, body.Route, ct);
    }
}
