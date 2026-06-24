using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Domain.Entities;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class ReferralRulesAppService(
    ParrainageDbContext db,
    ReferralRuleResolver ruleResolver) : IReferralRulesAppService
{
    public async Task<IReadOnlyList<ReferralRuleDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.ReferralRules.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ReferralRuleCatalogDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var rows = await db.ReferralRules.AsNoTracking()
            .Where(r =>
                r.Status == "ACTIVE" &&
                r.Type == ReferralRuleResolver.PositionRuleType &&
                r.Target != null &&
                r.Target != "")
            .OrderBy(r => r.Target)
            .ToListAsync(ct);

        return rows.Select(r => new ReferralRuleCatalogDto
        {
            RuleId = r.Id,
            Target = r.Target!,
            Value = r.Value,
            MinDurationMonths = r.MinDurationMonths,
        }).ToList();
    }

    public async Task<ReferralRuleDto> UpsertAsync(string id, UpsertRuleRequest body, CancellationToken ct = default)
    {
        var entity = await db.ReferralRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
        {
            entity = new ReferralRuleEntity { Id = id, CreatedAt = DateTimeOffset.UtcNow };
            db.ReferralRules.Add(entity);
        }

        if (body.Name != null) entity.Name = body.Name;
        if (body.Type != null) entity.Type = body.Type;
        if (body.Value.HasValue) entity.Value = body.Value.Value;
        if (body.Target != null) entity.Target = body.Target;
        if (body.MinDurationMonths.HasValue) entity.MinDurationMonths = body.MinDurationMonths.Value;
        if (body.Status != null) entity.Status = body.Status;

        if (entity.Type == ReferralRuleResolver.PositionRuleType)
        {
            if (entity.MinDurationMonths <= 0)
                entity.MinDurationMonths = DefaultSystemConfig.MinDurationMonths;
            await ruleResolver.ValidateUniqueActiveTargetAsync(
                entity.Target,
                entity.Type,
                entity.Status == "ACTIVE" ? entity.Id : null,
                ct);
        }

        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await db.ReferralRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
            return false;
        db.ReferralRules.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
