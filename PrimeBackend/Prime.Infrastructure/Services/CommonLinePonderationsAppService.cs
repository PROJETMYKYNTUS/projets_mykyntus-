using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class CommonLinePonderationsAppService(
    PrimeDbContext db,
    PrimeOrgScopeService org,
    ICommonLinePonderationResolver resolver) : ICommonLinePonderationsAppService
{
    public Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> GetCelluleEffectiveAsync(
        string celluleId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        IReadOnlyList<TemplateCommonLineHint>? templateLines = null,
        CancellationToken ct = default) =>
        GetEffectiveAsync(
            serviceId: null,
            celluleId: celluleId,
            supervisorUserId,
            templateId,
            effectiveAt,
            templateLines,
            ct);

    public async Task<IReadOnlyList<CommonLinePonderationDto>> PutCelluleAsync(
        string celluleId,
        string supervisorUserId,
        PutCommonLinePonderationsRequest body,
        CancellationToken ct = default)
    {
        var cid = RequireId(celluleId, "celluleId");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, cid, ct);
        if (!await db.Cellules.AsNoTracking().AnyAsync(c => c.Id == cid, ct))
            throw new KeyNotFoundException("Cellule introuvable.");

        return await PutScopeAsync(
            CommonLinePonderationScopes.Cellule,
            cid,
            supervisorUserId,
            body,
            replaceAll: true,
            ct);
    }

    public async Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> GetServiceEffectiveAsync(
        string serviceId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        IReadOnlyList<TemplateCommonLineHint>? templateLines = null,
        CancellationToken ct = default)
    {
        var sid = RequireId(serviceId, "serviceId");
        var celluleId = await org.GetCelluleIdForServiceAsync(sid, ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, celluleId, ct);
        var tid = (templateId ?? "").Trim();
        var at = CommonLinePonderationPeriod.StartOfUtcDay(effectiveAt ?? DateTimeOffset.UtcNow);
        var hints = templateLines is { Count: > 0 }
            ? templateLines
            : await resolver.LoadHintsFromLatestDraftAsync(celluleId, tid, ct);
        var prevHints = await resolver.BuildPreviousPeriodHintsAsync(celluleId, tid, at, ct);
        return await resolver.ResolveAsync(
            sid,
            celluleId,
            tid,
            at,
            hints,
            prevHints,
            ct);
    }

    public async Task<IReadOnlyList<CommonLinePonderationDto>> PutServiceAsync(
        string serviceId,
        string supervisorUserId,
        PutCommonLinePonderationsRequest body,
        bool replaceAll = false,
        CancellationToken ct = default)
    {
        var sid = RequireId(serviceId, "serviceId");
        var celluleId = await org.GetCelluleIdForServiceAsync(sid, ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, celluleId, ct);

        return await PutScopeAsync(
            CommonLinePonderationScopes.Service,
            sid,
            supervisorUserId,
            body,
            replaceAll,
            ct);
    }

    public async Task DeleteServiceOverrideAsync(
        string serviceId,
        string templateStableId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        CancellationToken ct = default)
    {
        var sid = RequireId(serviceId, "serviceId");
        var stable = RequireId(templateStableId, "templateStableId");
        var celluleId = await org.GetCelluleIdForServiceAsync(sid, ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, celluleId, ct);

        var tid = (templateId ?? "").Trim();
        var at = CommonLinePonderationPeriod.StartOfUtcDay(effectiveAt ?? DateTimeOffset.UtcNow);
        var rows = await db.CommonLinePonderations
            .Where(x =>
                x.ScopeType == CommonLinePonderationScopes.Service &&
                x.ScopeId == sid &&
                x.TemplateStableId == stable &&
                (tid.Length == 0 || x.TemplateId == tid || x.TemplateId == "") &&
                x.EffectiveFrom <= at &&
                (x.EffectiveTo == null || x.EffectiveTo >= at))
            .ToListAsync(ct);

        if (rows.Count == 0)
            throw new KeyNotFoundException("Aucune surcharge service active pour cet indicateur.");

        foreach (var row in rows)
            CloseOrRemove(row, at);

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> ConsolidateIdenticalServiceOverridesAsync(
        string celluleId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        CancellationToken ct = default)
    {
        var cid = RequireId(celluleId, "celluleId");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, cid, ct);
        var serviceIds = await db.Services.AsNoTracking()
            .Where(s => s.CelluleId == cid)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (serviceIds.Count < 2)
            return 0;

        var tid = (templateId ?? "").Trim();
        var at = CommonLinePonderationPeriod.StartOfUtcDay(effectiveAt ?? DateTimeOffset.UtcNow);
        var perService = new List<IReadOnlyList<EffectiveCommonLinePonderationDto>>();
        foreach (var sid in serviceIds)
        {
            perService.Add(await resolver.ResolveAsync(sid, cid, tid, at, null, null, ct));
        }

        var first = perService[0]
            .Where(x => x.SourceScope is CommonLinePonderationSources.Service or CommonLinePonderationSources.Cellule)
            .ToDictionary(x => x.TemplateStableId, StringComparer.OrdinalIgnoreCase);

        var identicalKeys = new List<EffectiveCommonLinePonderationDto>();
        foreach (var (stable, sample) in first)
        {
            var allMatch = perService.All(list =>
            {
                var hit = list.FirstOrDefault(x =>
                    string.Equals(x.TemplateStableId, stable, StringComparison.OrdinalIgnoreCase));
                return hit is not null &&
                       hit.PonderationPrimePct == sample.PonderationPrimePct &&
                       hit.PonderationChallengePct == sample.PonderationChallengePct &&
                       hit.SourceScope is CommonLinePonderationSources.Service or CommonLinePonderationSources.Cellule;
            });
            if (allMatch)
                identicalKeys.Add(sample);
        }

        if (identicalKeys.Count == 0)
            return 0;

        await PutScopeAsync(
            CommonLinePonderationScopes.Cellule,
            cid,
            supervisorUserId,
            new PutCommonLinePonderationsRequest
            {
                TemplateId = tid,
                EffectiveFrom = at,
                Items = identicalKeys.Select(x => new PutCommonLinePonderationItem
                {
                    TemplateStableId = x.TemplateStableId,
                    Label = x.Label,
                    Contract = x.Contract,
                    SortOrder = x.SortOrder,
                    PonderationPrimePct = x.PonderationPrimePct,
                    PonderationChallengePct = x.PonderationChallengePct,
                }).ToList(),
            },
            replaceAll: false,
            ct);

        var closed = 0;
        foreach (var sid in serviceIds)
        {
            foreach (var item in identicalKeys)
            {
                var rows = await db.CommonLinePonderations
                    .Where(x =>
                        x.ScopeType == CommonLinePonderationScopes.Service &&
                        x.ScopeId == sid &&
                        x.TemplateStableId == item.TemplateStableId &&
                        (tid.Length == 0 || x.TemplateId == tid || x.TemplateId == "") &&
                        x.EffectiveFrom <= at &&
                        (x.EffectiveTo == null || x.EffectiveTo >= at))
                    .ToListAsync(ct);
                foreach (var row in rows)
                {
                    if (row.PonderationPrimePct == item.PonderationPrimePct &&
                        row.PonderationChallengePct == item.PonderationChallengePct)
                    {
                        CloseOrRemove(row, at);
                        closed++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return closed;
    }

    private async Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> GetEffectiveAsync(
        string? serviceId,
        string celluleId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        IReadOnlyList<TemplateCommonLineHint>? templateLines,
        CancellationToken ct)
    {
        var cid = RequireId(celluleId, "celluleId");
        await EnsureCanConfigureCelluleAsync(supervisorUserId, cid, ct);
        var tid = (templateId ?? "").Trim();
        var at = CommonLinePonderationPeriod.StartOfUtcDay(effectiveAt ?? DateTimeOffset.UtcNow);

        var hints = templateLines is { Count: > 0 }
            ? templateLines
            : await resolver.LoadHintsFromLatestDraftAsync(cid, tid, ct);
        var prevHints = await resolver.BuildPreviousPeriodHintsAsync(cid, tid, at, ct);

        return await resolver.ResolveAsync(
            serviceId,
            cid,
            tid,
            at,
            hints,
            prevHints,
            ct);
    }

    private async Task<IReadOnlyList<CommonLinePonderationDto>> PutScopeAsync(
        string scopeType,
        string scopeId,
        string actorUserId,
        PutCommonLinePonderationsRequest body,
        bool replaceAll,
        CancellationToken ct)
    {
        var templateId = (body.TemplateId ?? "").Trim();
        if (templateId.Length == 0 && scopeType == CommonLinePonderationScopes.Cellule)
            throw new ArgumentException("templateId est requis.");

        var effectiveFrom = CommonLinePonderationPeriod.StartOfUtcDay(
            body.EffectiveFrom ?? CommonLinePonderationPeriod.DefaultEffectiveFromForNewVersion());
        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incoming = new List<PutCommonLinePonderationItem>();
        foreach (var item in body.Items.OrderBy(i => i.SortOrder))
        {
            var stable = (item.TemplateStableId ?? "").Trim();
            if (stable.Length == 0) continue;
            if (!seen.Add(stable)) continue;
            incoming.Add(new PutCommonLinePonderationItem
            {
                TemplateStableId = stable,
                Label = (item.Label ?? "").Trim(),
                Contract = (item.Contract ?? "").Trim(),
                SortOrder = item.SortOrder,
                PonderationPrimePct = CommonLinePonderationPeriod.NormalizePct(item.PonderationPrimePct),
                PonderationChallengePct = CommonLinePonderationPeriod.NormalizePct(item.PonderationChallengePct),
            });
        }

        var existing = await db.CommonLinePonderations
            .Where(x =>
                x.ScopeType == scopeType &&
                x.ScopeId == scopeId &&
                (templateId.Length == 0 || x.TemplateId == templateId || x.TemplateId == ""))
            .ToListAsync(ct);

        if (replaceAll)
        {
            var incomingKeys = incoming.Select(i => i.TemplateStableId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in existing.Where(x =>
                         x.EffectiveTo == null &&
                         x.EffectiveFrom <= effectiveFrom &&
                         !incomingKeys.Contains(x.TemplateStableId)))
            {
                CloseOrRemove(row, effectiveFrom);
            }
        }

        foreach (var item in incoming)
        {
            var versions = existing
                .Where(x => string.Equals(x.TemplateStableId, item.TemplateStableId, StringComparison.OrdinalIgnoreCase) &&
                            (templateId.Length == 0 || x.TemplateId == templateId || x.TemplateId == ""))
                .OrderBy(x => x.EffectiveFrom)
                .ToList();

            var sameDay = versions.FirstOrDefault(x => x.EffectiveFrom == effectiveFrom && x.EffectiveTo == null);
            if (sameDay is not null)
            {
                sameDay.Label = item.Label;
                sameDay.Contract = item.Contract;
                sameDay.SortOrder = item.SortOrder;
                sameDay.PonderationPrimePct = item.PonderationPrimePct;
                sameDay.PonderationChallengePct = item.PonderationChallengePct;
                continue;
            }

            var active = versions.FirstOrDefault(x =>
                x.EffectiveTo == null || x.EffectiveTo >= effectiveFrom);
            if (active is not null &&
                active.PonderationPrimePct == item.PonderationPrimePct &&
                active.PonderationChallengePct == item.PonderationChallengePct &&
                active.EffectiveTo == null)
            {
                active.Label = item.Label;
                active.Contract = item.Contract;
                active.SortOrder = item.SortOrder;
                continue;
            }

            if (active is not null)
                CloseOrRemove(active, effectiveFrom);

            var created = new CommonLinePonderationEntity
            {
                Id = Guid.NewGuid(),
                ScopeType = scopeType,
                ScopeId = scopeId,
                TemplateId = templateId,
                TemplateStableId = item.TemplateStableId,
                Label = item.Label,
                Contract = item.Contract,
                SortOrder = item.SortOrder,
                PonderationPrimePct = item.PonderationPrimePct,
                PonderationChallengePct = item.PonderationChallengePct,
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                CreatedBy = actorUserId.Trim(),
                CreatedAt = now,
            };

            EnsureNoOverlap(existing, created);
            db.CommonLinePonderations.Add(created);
            existing.Add(created);
        }

        await db.SaveChangesAsync(ct);

        var stored = await db.CommonLinePonderations.AsNoTracking()
            .Where(x =>
                x.ScopeType == scopeType &&
                x.ScopeId == scopeId &&
                (templateId.Length == 0 || x.TemplateId == templateId) &&
                x.EffectiveTo == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.TemplateStableId)
            .ToListAsync(ct);
        return stored.ConvertAll(MapDeclared);
    }

    private void CloseOrRemove(CommonLinePonderationEntity row, DateTimeOffset newFrom)
    {
        var closeAt = newFrom.AddTicks(-1);
        if (closeAt < row.EffectiveFrom)
            db.CommonLinePonderations.Remove(row);
        else
            row.EffectiveTo = closeAt;
    }

    private void EnsureNoOverlap(
        IEnumerable<CommonLinePonderationEntity> existing,
        CommonLinePonderationEntity candidate)
    {
        var end = candidate.EffectiveTo ?? DateTimeOffset.MaxValue;
        foreach (var other in existing)
        {
            if (other.Id == candidate.Id) continue;
            if (db.Entry(other).State == EntityState.Deleted) continue;
            if (!string.Equals(other.ScopeType, candidate.ScopeType, StringComparison.Ordinal)) continue;
            if (!string.Equals(other.ScopeId, candidate.ScopeId, StringComparison.Ordinal)) continue;
            if (!string.Equals(other.TemplateStableId, candidate.TemplateStableId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(other.TemplateId, candidate.TemplateId, StringComparison.Ordinal)) continue;

            var otherEnd = other.EffectiveTo ?? DateTimeOffset.MaxValue;
            if (candidate.EffectiveFrom <= otherEnd && other.EffectiveFrom <= end)
                throw new InvalidOperationException(
                    "Les versions temporelles de pondération ne doivent pas se chevaucher.");
        }
    }

    private async Task EnsureCanConfigureCelluleAsync(string supervisorUserId, string celluleId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId requis.");

        var emp = await org.GetEmployeeAsync(supervisorUserId, ct);
        var role = (emp?.Role ?? "").Trim();
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "RH", StringComparison.OrdinalIgnoreCase))
            return;

        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, celluleId, ct))
            throw new UnauthorizedAccessException("Accès refusé pour ce périmètre.");
    }

    private static string RequireId(string? value, string name)
    {
        var t = (value ?? "").Trim();
        if (t.Length == 0)
            throw new ArgumentException($"{name} est requis.");
        return t;
    }

    private static CommonLinePonderationDto MapDeclared(CommonLinePonderationEntity e) =>
        new()
        {
            Id = e.Id,
            ScopeType = e.ScopeType,
            ScopeId = e.ScopeId,
            TemplateId = e.TemplateId,
            TemplateStableId = e.TemplateStableId,
            Label = e.Label,
            Contract = e.Contract,
            SortOrder = e.SortOrder,
            PonderationPrimePct = e.PonderationPrimePct,
            PonderationChallengePct = e.PonderationChallengePct,
            EffectiveFrom = e.EffectiveFrom,
            EffectiveTo = e.EffectiveTo,
            CreatedBy = e.CreatedBy,
            CreatedAt = e.CreatedAt,
        };
}
