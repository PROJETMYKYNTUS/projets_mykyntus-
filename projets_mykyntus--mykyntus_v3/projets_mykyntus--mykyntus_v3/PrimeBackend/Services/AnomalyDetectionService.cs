using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Détection d'anomalies sur les fiches PRIME — terminaux et SLA alignés sur le workflow en base.
/// </summary>
public sealed class AnomalyDetectionService(PrimeDbContext db, PrimeValidationWorkflowRuntime wfRuntime)
{
    private const decimal MoneyTolerance = 0.01m;
    private const decimal AberrantPrimeThreshold = 10000m;

    public async Task<int> RecomputeAllAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var detected = new List<AnomalyEntity>();
        var slaGlobal = await GetGlobalSlaHoursAsync(ct);
        var terminals = await wfRuntime.GetTerminalStatusesAsync(ct);

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking().ToListAsync(ct);

        foreach (var f in fiches)
        {
            if (f.TotalAmount is null || (f.PrimeAmount is null && f.ChallengeAmount is null)) continue;
            var sum = (f.PrimeAmount ?? 0m) + (f.ChallengeAmount ?? 0m);
            if (Math.Abs((f.TotalAmount ?? 0m) - sum) > MoneyTolerance)
            {
                detected.Add(BuildAnomaly(f, now, "ComputationMismatch", "High",
                    $"Total ({f.TotalAmount:F2}) ≠ Prime + Challenge ({sum:F2})",
                    JsonSerializer.Serialize(new { f.PrimeAmount, f.ChallengeAmount, f.TotalAmount })));
            }
        }

        var duplicates = fiches
            .GroupBy(f => new { f.EmployeeId, f.Period })
            .Where(g => g.Count() > 1);
        foreach (var grp in duplicates)
        {
            foreach (var f in grp)
            {
                detected.Add(BuildAnomaly(f, now, "DuplicateFiche", "Critical",
                    $"Plusieurs fiches détectées pour l'employé {f.EmployeeId} sur la période {f.Period} ({grp.Count()} occurrences).",
                    JsonSerializer.Serialize(new { count = grp.Count(), ids = grp.Select(x => x.Id) })));
            }
        }

        foreach (var f in fiches)
        {
            var primeNeg = (f.PrimeAmount ?? 0m) < 0m;
            var challengeNeg = (f.ChallengeAmount ?? 0m) < 0m;
            var aberrant = (f.PrimeAmount ?? 0m) > AberrantPrimeThreshold;
            if (primeNeg || challengeNeg || aberrant)
            {
                var sev = aberrant ? "High" : "Medium";
                detected.Add(BuildAnomaly(f, now, "OutOfRange", sev,
                    primeNeg ? $"Montant prime négatif ({f.PrimeAmount:F2})." :
                    challengeNeg ? $"Montant challenge négatif ({f.ChallengeAmount:F2})." :
                    $"Montant prime aberrant ({f.PrimeAmount:F2} > {AberrantPrimeThreshold:F2}).",
                    JsonSerializer.Serialize(new { f.PrimeAmount, f.ChallengeAmount })));
            }
        }

        foreach (var f in fiches)
        {
            if (f.ValidationStatus == PrimeValidationWorkflowService.Pending ||
                f.ValidationStatus == PrimeValidationWorkflowService.Rejected ||
                terminals.Contains(f.ValidationStatus))
                continue;
            if (string.IsNullOrWhiteSpace(f.LastApproverUserId))
            {
                detected.Add(BuildAnomaly(f, now, "MissingApprover", "High",
                    $"Statut « {f.ValidationStatus} » sans LastApproverUserId renseigné.",
                    JsonSerializer.Serialize(new { f.ValidationStatus, f.LastApprovedAt })));
            }
        }

        foreach (var f in fiches)
        {
            if (terminals.Contains(f.ValidationStatus)) continue;
            var stepSla = await wfRuntime.GetSlaHoursForCurrentStepAsync(f.ValidationStatus, ct);
            var hours = stepSla ?? slaGlobal;
            if (hours <= 0) continue;
            var staleThreshold = now.AddHours(-hours * 3);
            if (f.UpdatedAt < staleThreshold)
            {
                detected.Add(BuildAnomaly(f, now, "StaleValidation", "Medium",
                    $"Fiche bloquée en « {f.ValidationStatus} » sans mise à jour depuis {(now - f.UpdatedAt).TotalHours:F0}h.",
                    JsonSerializer.Serialize(new { f.UpdatedAt, slaHours = hours })));
            }
        }

        foreach (var f in fiches)
        {
            if (f.ValidationStatus == PrimeValidationWorkflowService.Pending ||
                f.ValidationStatus == PrimeValidationWorkflowService.Rejected)
                continue;
            if (string.IsNullOrWhiteSpace(f.ServiceId) || string.IsNullOrWhiteSpace(f.CelluleId))
            {
                detected.Add(BuildAnomaly(f, now, "InvalidScope", "High",
                    "Fiche validée sans ServiceId ou CelluleId.",
                    JsonSerializer.Serialize(new { f.ServiceId, f.CelluleId })));
            }
        }

        var hasActiveSteps = await db.WorkflowSteps.AsNoTracking().AnyAsync(s => s.IsActive, ct);
        if (hasActiveSteps)
        {
            foreach (var f in fiches)
            {
                if (f.ValidationStatus == PrimeValidationWorkflowService.Pending ||
                    f.ValidationStatus == PrimeValidationWorkflowService.Rejected)
                    continue;
                var hasFrom = await db.WorkflowSteps.AsNoTracking()
                    .AnyAsync(s => s.IsActive && s.FromStatus == f.ValidationStatus, ct);
                if (!hasFrom && !terminals.Contains(f.ValidationStatus))
                {
                    detected.Add(BuildAnomaly(f, now, "WorkflowBlocked", "Critical",
                        $"Aucune transition active depuis le statut « {f.ValidationStatus} » — workflow bloqué.",
                        null));
                }
            }
        }

        return await UpsertAnomaliesAsync(detected, now, ct);
    }

    public async Task RecomputeForFicheAsync(Guid ficheId, CancellationToken ct = default)
    {
        var f = await db.EmployeePrimeServiceFiches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ficheId, ct);
        if (f == null) return;

        var now = DateTimeOffset.UtcNow;
        var slaGlobal = await GetGlobalSlaHoursAsync(ct);
        var terminals = await wfRuntime.GetTerminalStatusesAsync(ct);
        var detected = new List<AnomalyEntity>();

        if (f.TotalAmount is not null && (f.PrimeAmount is not null || f.ChallengeAmount is not null))
        {
            var sum = (f.PrimeAmount ?? 0m) + (f.ChallengeAmount ?? 0m);
            if (Math.Abs((f.TotalAmount ?? 0m) - sum) > MoneyTolerance)
                detected.Add(BuildAnomaly(f, now, "ComputationMismatch", "High",
                    $"Total ({f.TotalAmount:F2}) ≠ Prime + Challenge ({sum:F2})", null));
        }
        if ((f.PrimeAmount ?? 0m) < 0m || (f.ChallengeAmount ?? 0m) < 0m || (f.PrimeAmount ?? 0m) > AberrantPrimeThreshold)
            detected.Add(BuildAnomaly(f, now, "OutOfRange", "Medium", "Montant prime/challenge hors bornes.", null));
        if (f.ValidationStatus != PrimeValidationWorkflowService.Pending &&
            f.ValidationStatus != PrimeValidationWorkflowService.Rejected &&
            !terminals.Contains(f.ValidationStatus) &&
            string.IsNullOrWhiteSpace(f.LastApproverUserId))
            detected.Add(BuildAnomaly(f, now, "MissingApprover", "High", "Statut sans LastApproverUserId.", null));
        if (!terminals.Contains(f.ValidationStatus))
        {
            var stepSla = await wfRuntime.GetSlaHoursForCurrentStepAsync(f.ValidationStatus, ct);
            var hours = stepSla ?? slaGlobal;
            if (hours > 0 && f.UpdatedAt < now.AddHours(-hours * 3))
                detected.Add(BuildAnomaly(f, now, "StaleValidation", "Medium", "Fiche en attente trop longtemps.", null));
        }
        if (f.ValidationStatus != PrimeValidationWorkflowService.Pending &&
            (string.IsNullOrWhiteSpace(f.ServiceId) || string.IsNullOrWhiteSpace(f.CelluleId)))
            detected.Add(BuildAnomaly(f, now, "InvalidScope", "High", "Périmètre incomplet.", null));
        if (await db.WorkflowSteps.AsNoTracking().AnyAsync(s => s.IsActive, ct))
        {
            if (f.ValidationStatus != PrimeValidationWorkflowService.Pending &&
                f.ValidationStatus != PrimeValidationWorkflowService.Rejected)
            {
                var hasFrom = await db.WorkflowSteps.AsNoTracking()
                    .AnyAsync(s => s.IsActive && s.FromStatus == f.ValidationStatus, ct);
                if (!hasFrom && !terminals.Contains(f.ValidationStatus))
                    detected.Add(BuildAnomaly(f, now, "WorkflowBlocked", "Critical",
                        $"Workflow bloqué sur « {f.ValidationStatus} ».", null));
            }
        }

        await UpsertAnomaliesAsync(detected, now, ct, scopedTargetId: f.Id.ToString());
    }

    private static AnomalyEntity BuildAnomaly(EmployeePrimeServiceFicheEntity f, DateTimeOffset now, string type, string severity, string description, string? contextJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            DetectedAt = now,
            UpdatedAt = now,
            Type = type,
            Severity = severity,
            Status = "Open",
            Description = description,
            TargetEntityType = nameof(EmployeePrimeServiceFicheEntity),
            TargetEntityId = f.Id.ToString(),
            Period = f.Period,
            ServiceId = f.ServiceId,
            CelluleId = f.CelluleId,
            ContextJson = contextJson,
        };

    private async Task<int> UpsertAnomaliesAsync(List<AnomalyEntity> detected, DateTimeOffset now, CancellationToken ct, string? scopedTargetId = null)
    {
        var existingQuery = db.Anomalies.AsQueryable();
        if (scopedTargetId is not null)
            existingQuery = existingQuery.Where(a => a.TargetEntityId == scopedTargetId);
        var existing = await existingQuery.ToListAsync(ct);

        var detectedKeys = detected
            .Select(a => (a.Type, a.TargetEntityType, a.TargetEntityId))
            .ToHashSet();

        int upserts = 0;
        foreach (var d in detected)
        {
            var match = existing.FirstOrDefault(a =>
                a.Type == d.Type && a.TargetEntityType == d.TargetEntityType && a.TargetEntityId == d.TargetEntityId);
            if (match == null)
            {
                db.Anomalies.Add(d);
                upserts++;
            }
            else if (match.Status is "Open" or "InReview")
            {
                match.Description = d.Description;
                match.Severity = d.Severity;
                match.ContextJson = d.ContextJson;
                match.UpdatedAt = now;
                upserts++;
            }
        }

        foreach (var a in existing)
        {
            var key = (a.Type, a.TargetEntityType, a.TargetEntityId);
            if (a.Status == "Open" && !detectedKeys.Contains(key))
            {
                a.Status = "Resolved";
                a.ResolvedAt = now;
                a.ResolutionNote = "Auto-resolved: condition disparue.";
                a.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
        return upserts;
    }

    private async Task<int> GetGlobalSlaHoursAsync(CancellationToken ct)
    {
        var row = await db.WorkflowGlobalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return row?.GlobalSlaHours ?? 72;
    }
}
