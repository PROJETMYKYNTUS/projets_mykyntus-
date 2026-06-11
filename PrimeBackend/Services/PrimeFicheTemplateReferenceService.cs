using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Services;

/// <summary>Compte les fiches / brouillons liés à un templateId (protection suppression).</summary>
public sealed class PrimeFicheTemplateReferenceService(PrimeDbContext db, PrimeOrgScopeService org)
{
    private static readonly string[] ValidatedStatuses =
    [
        "Superviseur Approved",
        "Chef de projet Approved",
        "RH Approved",
    ];

    public async Task<PrimeFicheTemplateUsageDto> GetUsageAsync(
        string templateId,
        string supervisorUserId,
        string? role,
        CancellationToken ct = default)
    {
        var tid = templateId.Trim();
        var sup = supervisorUserId.Trim();
        var isAdmin = string.Equals((role ?? "").Trim(), "Admin", StringComparison.Ordinal);

        IQueryable<SupervisorCellulePrimeDraftEntity> drafts = db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.TemplateId == tid);
        IQueryable<EmployeePrimeServiceFicheEntity> fiches = db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.CellulePrimeDraft.TemplateId == tid);

        if (!isAdmin)
        {
            var celluleIds = await org.GetSupervisedCelluleIdsAsync(sup, ct);
            if (celluleIds.Count == 0)
            {
                return new PrimeFicheTemplateUsageDto { TemplateId = tid };
            }

            drafts = drafts.Where(d => d.SupervisorUserId == sup && celluleIds.Contains(d.CelluleId));
            fiches = fiches.Where(f => f.SupervisorUserId == sup && celluleIds.Contains(f.CelluleId));
        }

        var commonsCount = await drafts.CountAsync(ct);
        var pilotCount = await fiches.CountAsync(ct);
        var frozenCount = await fiches.CountAsync(f => f.DetailGridFrozenAt != null, ct);
        var validatedCount = await fiches.CountAsync(f => ValidatedStatuses.Contains(f.ValidationStatus), ct);

        return new PrimeFicheTemplateUsageDto
        {
            TemplateId = tid,
            CommonsDraftCount = commonsCount,
            PilotFicheCount = pilotCount,
            FrozenPilotFicheCount = frozenCount,
            ValidatedPilotFicheCount = validatedCount,
        };
    }

    /// <summary>
    /// Vrai si un brouillon fiche commune du superviseur porte déjà ce nom affiché
    /// (normalisation insensible à la casse / espaces).
    /// </summary>
    public async Task<bool> IsDisplayNameTakenAsync(
        string supervisorUserId,
        string displayName,
        string? excludeTemplateId,
        CancellationToken ct = default)
    {
        var key = NormalizeDisplayName(displayName);
        if (string.IsNullOrEmpty(key)) return false;

        var sup = supervisorUserId.Trim();
        var celluleIds = await org.GetSupervisedCelluleIdsAsync(sup, ct);
        if (celluleIds.Count == 0) return false;

        var exclude = (excludeTemplateId ?? "").Trim();
        var names = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == sup && celluleIds.Contains(d.CelluleId))
            .Where(d => exclude.Length == 0 || d.TemplateId != exclude)
            .Select(d => d.TemplateDisplayName)
            .ToListAsync(ct);

        return names.Any(n => NormalizeDisplayName(n) == key);
    }

    private static string NormalizeDisplayName(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return "";
        var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts).ToLower(CultureInfo.InvariantCulture);
    }
}
