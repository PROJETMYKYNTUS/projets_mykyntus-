using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Service de détection d'anomalies sur les fiches PRIME (Phase 1.5).
///
/// 6 règles métier :
///   R1 (ComputationMismatch)  : TotalAmount renseigné ≠ PrimeAmount + ChallengeAmount (tolérance 0.01).
///   R2 (DuplicateFiche)       : plusieurs fiches pour le même (EmployeeId, Period) — devrait être unique.
///   R3 (OutOfRange)           : montant prime ou challenge négatif, ou prime > seuil aberrant (10 000 par défaut).
///   R4 (MissingApprover)      : statut ≠ Pending et ≠ Rejected sans LastApproverUserId.
///   R5 (StaleValidation)      : fiche en cours (≠ RH Approved et ≠ Rejected) sans mise à jour depuis SLA global × 3.
///   R6 (InvalidScope)         : ServiceId / CelluleId vides alors que la fiche est validée.
/// </summary>
public sealed class AnomalyDetectionService(PrimeDbContext db)
{
    private const decimal MoneyTolerance = 0.01m;
    private const decimal AberrantPrimeThreshold = 10000m;

    /// <summary>Recalcule l'ensemble des anomalies sur le périmètre courant (toute la base). Idempotent : met à jour les Open existantes, ré-crée si manquantes, marque Resolved les disparues qui étaient Open.</summary>
    public async Task<int> RecomputeAllAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var detected = new List<AnomalyEntity>();
        var sla = await GetGlobalSlaHoursAsync(ct);

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking().ToListAsync(ct);

        // R1 ComputationMismatch
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

        // R2 DuplicateFiche
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

        // R3 OutOfRange
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

        // R4 MissingApprover
        foreach (var f in fiches)
        {
            if (f.ValidationStatus is PrimeValidationWorkflowService.Pending or PrimeValidationWorkflowService.Rejected) continue;
            if (string.IsNullOrWhiteSpace(f.LastApproverUserId))
            {
                detected.Add(BuildAnomaly(f, now, "MissingApprover", "High",
                    $"Statut « {f.ValidationStatus} » sans LastApproverUserId renseigné.",
                    JsonSerializer.Serialize(new { f.ValidationStatus, f.LastApprovedAt })));
            }
        }

        // R5 StaleValidation
        var staleThreshold = now.AddHours(-sla * 3);
        foreach (var f in fiches)
        {
            if (f.ValidationStatus is PrimeValidationWorkflowService.RhApproved or PrimeValidationWorkflowService.Rejected) continue;
            if (f.UpdatedAt < staleThreshold)
            {
                detected.Add(BuildAnomaly(f, now, "StaleValidation", "Medium",
                    $"Fiche bloquée en « {f.ValidationStatus} » sans mise à jour depuis {(now - f.UpdatedAt).TotalHours:F0}h.",
                    JsonSerializer.Serialize(new { f.UpdatedAt, slaHours = sla })));
            }
        }

        // R6 InvalidScope
        foreach (var f in fiches)
        {
            if (f.ValidationStatus == PrimeValidationWorkflowService.Pending) continue;
            if (string.IsNullOrWhiteSpace(f.ServiceId) || string.IsNullOrWhiteSpace(f.CelluleId))
            {
                detected.Add(BuildAnomaly(f, now, "InvalidScope", "High",
                    "Fiche validée sans ServiceId ou CelluleId.",
                    JsonSerializer.Serialize(new { f.ServiceId, f.CelluleId })));
            }
        }

        // Upsert idempotent : pour chaque (Type, TargetEntityType, TargetEntityId) on conserve la ligne existante si Open/InReview.
        return await UpsertAnomaliesAsync(detected, now, ct);
    }

    /// <summary>Réévalue les anomalies d'une seule fiche (appelé sur upsert/validation).</summary>
    public async Task RecomputeForFicheAsync(Guid ficheId, CancellationToken ct = default)
    {
        var f = await db.EmployeePrimeServiceFiches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ficheId, ct);
        if (f == null) return;

        var now = DateTimeOffset.UtcNow;
        var sla = await GetGlobalSlaHoursAsync(ct);
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
        if (f.ValidationStatus is not PrimeValidationWorkflowService.Pending and not PrimeValidationWorkflowService.Rejected
            && string.IsNullOrWhiteSpace(f.LastApproverUserId))
            detected.Add(BuildAnomaly(f, now, "MissingApprover", "High", "Statut sans LastApproverUserId.", null));
        if (f.ValidationStatus is not PrimeValidationWorkflowService.RhApproved and not PrimeValidationWorkflowService.Rejected
            && f.UpdatedAt < now.AddHours(-sla * 3))
            detected.Add(BuildAnomaly(f, now, "StaleValidation", "Medium", "Fiche en attente trop longtemps.", null));
        if (f.ValidationStatus != PrimeValidationWorkflowService.Pending
            && (string.IsNullOrWhiteSpace(f.ServiceId) || string.IsNullOrWhiteSpace(f.CelluleId)))
            detected.Add(BuildAnomaly(f, now, "InvalidScope", "High", "Périmètre incomplet.", null));

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
        // Si on est sur le scope d'une seule fiche, on supprime les Open existantes hors du set détecté pour cette fiche
        var existingQuery = db.Anomalies.AsQueryable();
        if (scopedTargetId is not null)
        {
            existingQuery = existingQuery.Where(a => a.TargetEntityId == scopedTargetId);
        }
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

        // Auto-résolution : Open existantes plus détectées
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
