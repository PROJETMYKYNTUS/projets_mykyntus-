using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class ParrainageAuditAppService(ParrainageDbContext db) : IParrainageAuditAppService
{
    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(int? take, CancellationToken ct = default)
    {
        var max = Math.Clamp(take ?? 500, 1, 2000);
        var rows = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(max)
            .ToListAsync(ct);
        return rows.Select(e => e.ToDto()).ToList();
    }

    public async Task<AuditLogDto> CreateAsync(CreateAuditRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.Action))
            throw new ArgumentException("action est requise.");

        var entity = new AuditLogEntryEntity
        {
            Id = $"audit-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Action = body.Action,
            UserId = body.UserId ?? "system",
            UserLabel = body.UserLabel ?? "Système",
            Timestamp = DateTimeOffset.UtcNow,
            Details = body.Details,
        };
        db.AuditLogs.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }
}
