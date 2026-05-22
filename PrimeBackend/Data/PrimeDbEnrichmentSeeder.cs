using System.Text.Json;
using Bogus;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

/// <summary>
/// Enrichissement idempotent de <c>prime_db</c> avec données fictives réalistes (démo / visualisation).
/// </summary>
public static class PrimeDbEnrichmentSeeder
{
    public const int Version = 3;
    private const string MarkerAction = "DemoSeedApplied";
    private const string EnrichTemplateId = "enrich-template-v2";
    private const string SupervisorId = "e9";
    private const string ChefDeProjetId = "e6";
    private const string ReferentId = "e8";

    private static readonly string[] EnrichEmployeeIds =
        Enumerable.Range(1, 15).Select(i => $"e-enrich-{i:D2}").ToArray();

    private static readonly (string Id, string Label, decimal PrimePct, decimal ChallengePct)[][] IndicatorsByService =
    [
        [
            ("nps-agents", "NPS agents (%)", 30m, 20m),
            ("aht-voice", "AHT voice (sec)", 25m, 15m),
            ("qa-score", "Score QA écoutes", 25m, 25m),
            ("fcr", "First contact resolution (%)", 20m, 20m),
        ],
        [
            ("nps-enquetes", "NPS enquêtes sortantes", 35m, 25m),
            ("taux-rappel", "Taux de rappel abouti", 30m, 20m),
            ("csat", "CSAT post-appel", 35m, 30m),
        ],
        [
            ("taux-retention", "Taux rétention client", 40m, 30m),
            ("delai-traitement", "Délai traitement réclamation (h)", 30m, 20m),
            ("engagements-tenus", "Engagements tenus (%)", 30m, 25m),
        ],
        [
            ("dispo-acd", "Disponibilité ACD (%)", 35m, 25m),
            ("incidents-p1", "Incidents P1 résolus < 4h", 35m, 30m),
            ("mttr", "MTTR réseau (min)", 30m, 20m),
        ],
    ];

    public static async Task<PrimeEnrichmentResult> EnrichAsync(
        PrimeDbContext db,
        bool force = false,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        if (!await db.Poles.AnyAsync(cancellationToken))
        {
            logger?.LogWarning("PRIME enrichissement ignoré : aucun pôle en base (seed initial absent).");
            return PrimeEnrichmentResult.Skipped("no_poles");
        }

        if (force)
        {
            var markers = await db.AuditLogs.Where(x => x.Action == MarkerAction).ToListAsync(cancellationToken);
            if (markers.Count > 0)
            {
                db.AuditLogs.RemoveRange(markers);
                await db.SaveChangesAsync(cancellationToken);
                logger?.LogInformation("PRIME enrichissement : marqueur(s) DemoSeedApplied supprimé(s) ({Count}).", markers.Count);
            }
        }
        else if (await IsVersionAppliedAsync(db, cancellationToken) && await HasEnrichmentDataAsync(db, cancellationToken))
        {
            logger?.LogInformation("PRIME enrichissement déjà appliqué (version {Version}).", Version);
            return PrimeEnrichmentResult.Skipped("already_applied");
        }

        var before = await SnapshotCountsAsync(db, cancellationToken);

        Randomizer.Seed = new Random(42);
        var faker = new Faker("fr");

        await SeedExtraEmployeesAsync(db, faker, cancellationToken);
        await SeedServiceIndicatorsAsync(db, cancellationToken);
        await SeedDraftsAndFichesAsync(db, faker, cancellationToken);
        await SeedAuditLogsAsync(db, faker, cancellationToken);
        await SeedAnomaliesAsync(db, cancellationToken);
        await SeedGlobalPoolApprovalsAsync(db, cancellationToken);
        await MarkVersionAppliedAsync(db, cancellationToken);

        var after = await SnapshotCountsAsync(db, cancellationToken);
        var result = PrimeEnrichmentResult.FromCounts(before, after);
        logger?.LogInformation(
            "PRIME enrichissement v{Version} terminé : +{Fiches} fiches, +{Audit} logs audit, +{Anomalies} anomalies, {EnrichEmployees} pilotes enrich.",
            Version,
            after.Fiches - before.Fiches,
            after.AuditLogs - before.AuditLogs,
            after.Anomalies - before.Anomalies,
            after.EnrichEmployees);
        return result;
    }

    public static async Task<bool> IsVersionAppliedAsync(PrimeDbContext db, CancellationToken ct = default) =>
        await IsVersionAppliedInternalAsync(db, ct);

    public static async Task<bool> HasEnrichmentDataAsync(PrimeDbContext db, CancellationToken ct = default) =>
        await db.Employees.AnyAsync(e => e.Id.StartsWith("e-enrich-"), ct)
        || await db.SupervisorCellulePrimeDrafts.AnyAsync(d => d.TemplateId == EnrichTemplateId, ct);

    public static async Task<PrimeEnrichmentCounts> SnapshotCountsAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        return new PrimeEnrichmentCounts(
            await db.Employees.CountAsync(e => e.Id.StartsWith("e-enrich-"), ct),
            await db.SupervisorCellulePrimeDrafts.CountAsync(d => d.TemplateId == EnrichTemplateId, ct),
            await db.EmployeePrimeServiceFiches.CountAsync(ct),
            await db.AuditLogs.CountAsync(x => x.Action != MarkerAction, ct),
            await db.Anomalies.CountAsync(ct),
            await db.ServicePrimeIndicators.CountAsync(ct));
    }

    private static async Task<bool> IsVersionAppliedInternalAsync(PrimeDbContext db, CancellationToken ct)
    {
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(x => x.Action == MarkerAction)
            .Select(x => x.DetailJson)
            .ToListAsync(ct);
        foreach (var detail in logs)
        {
            if (string.IsNullOrEmpty(detail)) continue;
            try
            {
                using var doc = JsonDocument.Parse(detail);
                if (doc.RootElement.TryGetProperty("version", out var v) && v.GetInt32() >= Version)
                    return true;
            }
            catch
            {
                // ignore malformed marker
            }
        }
        return false;
    }

    private static async Task MarkVersionAppliedAsync(PrimeDbContext db, CancellationToken ct)
    {
        if (await db.AuditLogs.AnyAsync(x => x.Action == MarkerAction && x.DetailJson != null && x.DetailJson.Contains($"\"version\":{Version}"), ct))
            return;

        db.AuditLogs.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            At = DateTimeOffset.UtcNow,
            UserId = "seed",
            UserDisplayName = "Enrichissement démo",
            Role = "Admin",
            Action = MarkerAction,
            EntityType = "PrimeDbEnrichment",
            EntityId = Version.ToString(),
            DetailJson = JsonSerializer.Serialize(new { version = Version, appliedAt = DateTimeOffset.UtcNow }),
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedExtraEmployeesAsync(PrimeDbContext db, Faker faker, CancellationToken ct)
    {
        var existing = await db.Employees.AsNoTracking()
            .Where(e => EnrichEmployeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);
        var missing = EnrichEmployeeIds.Except(existing).ToList();
        if (missing.Count == 0) return;

        var services = new[] { "c1", "c1", "c1", "c2", "c2", "c1", "c1", "c3", "c3", "c1", "c2", "c1", "c1", "c4", "c1" };
        var cellules = new[] { "p1", "p1", "p1", "p1", "p1", "p1", "p1", "p2", "p2", "p1", "p1", "p1", "p1", "p3", "p1" };

        var toAdd = new List<EmployeeEntity>();
        for (var i = 0; i < missing.Count; i++)
        {
            var id = missing[i];
            var idx = Array.IndexOf(EnrichEmployeeIds, id);
            var fn = faker.Name.FirstName();
            var ln = faker.Name.LastName();
            var svc = services[idx % services.Length];
            var cell = cellules[idx % cellules.Length];
            toAdd.Add(new EmployeeEntity
            {
                Id = id,
                FirstName = fn,
                LastName = ln,
                Role = "Pilote",
                ParentId = ReferentId,
                PoleId = cell == "p3" ? "d2" : "d1",
                CelluleId = cell,
                ServiceId = svc,
                Email = $"{fn.ToLowerInvariant()}.{ln.ToLowerInvariant()}@contactcentre.ma",
            });
        }

        db.Employees.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedServiceIndicatorsAsync(PrimeDbContext db, CancellationToken ct)
    {
        var serviceIds = new[] { "c1", "c2", "c3", "c4" };
        var now = DateTimeOffset.UtcNow;
        var toAdd = new List<ServicePrimeIndicatorEntity>();

        for (var s = 0; s < serviceIds.Length; s++)
        {
            var serviceId = serviceIds[s];
            var existingCount = await db.ServicePrimeIndicators.CountAsync(i => i.ServiceId == serviceId, ct);
            if (existingCount >= 3) continue;

            var defs = IndicatorsByService[s];
            var order = existingCount;
            foreach (var def in defs)
            {
                if (await db.ServicePrimeIndicators.AnyAsync(
                        i => i.ServiceId == serviceId && i.TemplateStableId == def.Id, ct))
                    continue;

                toAdd.Add(new ServicePrimeIndicatorEntity
                {
                    Id = Guid.NewGuid(),
                    ServiceId = serviceId,
                    SortOrder = order++,
                    Label = def.Label,
                    PonderationPrimePct = def.PrimePct,
                    PonderationChallengePct = def.ChallengePct,
                    IsActive = true,
                    TemplateStableId = def.Id,
                    CreatedAt = now,
                });
            }
        }

        if (toAdd.Count == 0) return;
        db.ServicePrimeIndicators.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDraftsAndFichesAsync(PrimeDbContext db, Faker faker, CancellationToken ct)
    {
        var periods = BuildEnrichmentPeriods();
        var celluleIds = new[] { ("p1", "d1"), ("p2", "d1") };
        var now = DateTimeOffset.UtcNow;

        var pilotIds = await db.Employees.AsNoTracking()
            .Where(e => e.Role == "Pilote" && (e.PoleId == "d1" || e.PoleId == "d2"))
            .Select(e => new { e.Id, e.ServiceId, e.CelluleId })
            .ToListAsync(ct);

        var statuses = new[]
        {
            PrimeValidationWorkflowService.AwaitingData,
            PrimeValidationWorkflowService.AwaitingData,
            PrimeValidationWorkflowService.Pending,
            PrimeValidationWorkflowService.ReferentTechniqueApproved,
            PrimeValidationWorkflowService.SuperviseurApproved,
            PrimeValidationWorkflowService.ChefDeProjetApproved,
            PrimeValidationWorkflowService.Rejected,
        };

        var ficheIndex = 0;
        foreach (var period in periods)
        {
            foreach (var (celluleId, _) in celluleIds)
            {
                var draft = await EnsureDraftAsync(db, celluleId, period, now, ct);
                if (draft is null) continue;

                var cellPilots = pilotIds.Where(p => p.CelluleId == celluleId).Take(8).ToList();
                if (cellPilots.Count == 0)
                    cellPilots = pilotIds.Take(6).ToList();

                foreach (var pilot in cellPilots)
                {
                    if (await db.EmployeePrimeServiceFiches.AnyAsync(
                            f => f.EmployeeId == pilot.Id && f.Period == period, ct))
                        continue;

                    var status = statuses[ficheIndex % statuses.Length];
                    ficheIndex++;
                    var (prime, challenge) = RandomAmounts(faker, ficheIndex);
                    var perf = BuildPerformanceJson(faker, ficheIndex);

                    var fiche = new EmployeePrimeServiceFicheEntity
                    {
                        Id = Guid.NewGuid(),
                        CellulePrimeDraftId = draft.Id,
                        SupervisorUserId = SupervisorId,
                        EmployeeId = pilot.Id,
                        ServiceId = pilot.ServiceId,
                        CelluleId = celluleId,
                        Period = period,
                        ServiceSaisieJson = perf,
                        FillingStatus = "Complete",
                        UpdatedAt = now.AddDays(-faker.Random.Int(1, 20)),
                        ValidationStatus = status,
                        PrimeAmount = prime,
                        ChallengeAmount = challenge,
                        TotalAmount = prime + challenge,
                    };

                    ApplyValidationMeta(fiche, status, now);
                    db.EmployeePrimeServiceFiches.Add(fiche);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<SupervisorCellulePrimeDraftEntity?> EnsureDraftAsync(
        PrimeDbContext db,
        string celluleId,
        string period,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await db.SupervisorCellulePrimeDrafts
            .FirstOrDefaultAsync(
                d => d.SupervisorUserId == SupervisorId
                     && d.CelluleId == celluleId
                     && d.Period == period
                     && d.TemplateId == EnrichTemplateId,
                ct);
        if (existing is not null) return existing;

        var rootPoleId = await db.Cellules.AsNoTracking()
            .Where(c => c.Id == celluleId)
            .Select(c => c.PoleId)
            .FirstOrDefaultAsync(ct) ?? celluleId;

        var draft = new SupervisorCellulePrimeDraftEntity
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = SupervisorId,
            RootPoleId = rootPoleId,
            CelluleId = celluleId,
            Period = period,
            TemplateId = EnrichTemplateId,
            TemplateDisplayName = $"Grille prime enrichie — {period}",
            TemplateFormatVersion = 1,
            Status = "Validated",
            SchemaJson = """{"fields":[{"id":"nps","label":"NPS (%)","type":"number"},{"id":"aht","label":"AHT","type":"number"}]}""",
            CelluleSaisieJson = """{"nps":72,"aht":285,"commentaire":"Saisie cellule démo enrichie"}""",
            TemplateCalcSnapshotJson = """{"previewSheetName":"Synthèse","calcSheets":[]}""",
            UpdatedAt = now,
        };

        if (period == BuildEnrichmentPeriods().Last() && celluleId == "p1")
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Synthèse");
            ws.Cell(1, 1).Value = "PRIME — synthèse globale enrichie";
            ws.Cell(2, 1).Value = period;
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            draft.GlobalPoolExcelContent = ms.ToArray();
            draft.GlobalPoolFileName = $"PRIME_synthese_{period}.xlsx";
            draft.GlobalPoolUploadedAt = now;
            draft.GlobalPoolUploadedByUserId = "seed-enrich";
        }

        db.SupervisorCellulePrimeDrafts.Add(draft);
        await db.SaveChangesAsync(ct);
        return draft;
    }

    private static void ApplyValidationMeta(EmployeePrimeServiceFicheEntity fiche, string status, DateTimeOffset now)
    {
        switch (status)
        {
            case PrimeValidationWorkflowService.ReferentTechniqueApproved:
                fiche.LastApproverUserId = ReferentId;
                fiche.LastApprovedAt = now.AddDays(-3);
                break;
            case PrimeValidationWorkflowService.SuperviseurApproved:
                fiche.LastApproverUserId = SupervisorId;
                fiche.LastApprovedAt = now.AddDays(-2);
                break;
            case PrimeValidationWorkflowService.ChefDeProjetApproved:
                fiche.LastApproverUserId = ChefDeProjetId;
                fiche.LastApprovedAt = now.AddDays(-1);
                break;
            case PrimeValidationWorkflowService.Rejected:
                fiche.RejectedByUserId = SupervisorId;
                fiche.RejectedAt = now.AddDays(-1);
                fiche.RejectionReason = "Écart ACD / saisie indicateur — resynchroniser avant validation.";
                break;
        }
    }

    private static (decimal prime, decimal challenge) RandomAmounts(Faker faker, int seed)
    {
        var r = new Random(42 + seed);
        var prime = (decimal)r.Next(800, 2200);
        var challenge = (decimal)r.Next(150, 600);
        return (prime, challenge);
    }

    private static string BuildPerformanceJson(Faker faker, int seed)
    {
        var r = new Random(42 + seed * 7);
        var completed = r.Next(8, 18);
        var total = completed + r.Next(2, 6);
        var objReached = r.Next(2, 6);
        var objTotal = objReached + r.Next(1, 4);
        return JsonSerializer.Serialize(new
        {
            completedTasks = completed,
            totalTasks = total,
            objectivesReached = objReached,
            totalObjectives = objTotal,
            nps = r.Next(35, 92),
            monthlyScores = new[]
            {
                new { month = "Jan", score = r.Next(65, 95) },
                new { month = "Fév", score = r.Next(65, 95) },
                new { month = "Mar", score = r.Next(65, 95) },
                new { month = "Avr", score = r.Next(65, 95) },
            },
        });
    }

    private static string[] BuildEnrichmentPeriods()
    {
        var current = $"{DateTime.UtcNow:yyyy-MM}";
        return ["2026-01", "2026-02", "2026-03", "2026-04", current];
    }

    private static async Task SeedAuditLogsAsync(PrimeDbContext db, Faker faker, CancellationToken ct)
    {
        var existing = await db.AuditLogs.CountAsync(x => x.Action != MarkerAction, ct);
        if (existing >= 50) return;

        var employees = await db.Employees.AsNoTracking().Take(20).ToListAsync(ct);
        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking().Take(40).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var actions = new[] { "ValidationApproved", "ValidationRejected", "OrgAssignmentChanged", "WorkflowConfigChanged" };
        var toAdd = new List<AuditLogEntity>();

        for (var i = 0; i < 80; i++)
        {
            var emp = employees[i % employees.Count];
            var fiche = fiches.Count > 0 ? fiches[i % fiches.Count] : null;
            var action = actions[i % actions.Length];
            toAdd.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                At = now.AddHours(-i * 3),
                UserId = emp.Id,
                UserDisplayName = $"{emp.FirstName} {emp.LastName}",
                Role = emp.Role,
                Action = action,
                EntityType = fiche is null ? "Employee" : "EmployeePrimeServiceFiche",
                EntityId = fiche?.Id.ToString() ?? emp.Id,
                DetailJson = JsonSerializer.Serialize(new
                {
                    period = fiche?.Period,
                    validationStatus = fiche?.ValidationStatus,
                    note = faker.Lorem.Sentence(6),
                }),
                IpAddress = $"10.0.{(i % 20)}.{(i % 200) + 1}",
            });
        }

        db.AuditLogs.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAnomaliesAsync(PrimeDbContext db, CancellationToken ct)
    {
        if (await db.Anomalies.CountAsync(ct) >= 10) return;

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking().Take(20).ToListAsync(ct);
        if (fiches.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var types = new[] { "ComputationMismatch", "StaleValidation", "OutOfRange", "MissingApprover", "DuplicateFiche" };
        var severities = new[] { "Critical", "High", "Medium", "Low" };
        var statuses = new[] { "Open", "InReview", "Resolved", "Ignored" };
        var toAdd = new List<AnomalyEntity>();

        for (var i = 0; i < 15; i++)
        {
            var f = fiches[i % fiches.Count];
            toAdd.Add(new AnomalyEntity
            {
                Id = Guid.NewGuid(),
                DetectedAt = now.AddDays(-i),
                UpdatedAt = now.AddDays(-i + 1),
                Type = types[i % types.Length],
                Severity = severities[i % severities.Length],
                Status = statuses[i % statuses.Length],
                Description = DescribeAnomaly(types[i % types.Length], f.Period),
                TargetEntityType = "EmployeePrimeServiceFiche",
                TargetEntityId = f.Id.ToString(),
                Period = f.Period,
                ServiceId = f.ServiceId,
                CelluleId = f.CelluleId,
                PoleId = "d1",
                ResolvedByUserId = statuses[i % statuses.Length] == "Resolved" ? ChefDeProjetId : null,
                ResolvedAt = statuses[i % statuses.Length] == "Resolved" ? now.AddDays(-i + 2) : null,
                ResolutionNote = statuses[i % statuses.Length] == "Resolved" ? "Corrigé après resynchronisation ACD." : null,
            });
        }

        db.Anomalies.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
    }

    private static string DescribeAnomaly(string type, string period) => type switch
    {
        "ComputationMismatch" => $"Montant prime incohérent avec la grille cellule pour {period}.",
        "StaleValidation" => $"Fiche en attente depuis plus de 72 h ({period}).",
        "OutOfRange" => $"Indicateur NPS hors bornes contractuelles ({period}).",
        "MissingApprover" => "Statut validé sans identifiant approbateur.",
        "DuplicateFiche" => "Doublon potentiel sur la même période.",
        _ => $"Anomalie détectée sur la période {period}.",
    };

    private static async Task SeedGlobalPoolApprovalsAsync(PrimeDbContext db, CancellationToken ct)
    {
        var draft = await db.SupervisorCellulePrimeDrafts
            .Where(d => d.GlobalPoolExcelContent != null)
            .OrderByDescending(d => d.Period)
            .FirstOrDefaultAsync(ct);
        if (draft is null) return;

        if (await db.GlobalPoolApprovals.AnyAsync(a => a.DraftId == draft.Id, ct)) return;

        var steps = await db.GlobalPoolWorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
        if (steps.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var managerStep = steps.FirstOrDefault(s => s.ApproverRole == "Manager") ?? steps[0];
        db.GlobalPoolApprovals.Add(new GlobalPoolApprovalEntity
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            StepId = managerStep.Id,
            UserId = "e10",
            ApprovedAt = now.AddDays(-1),
        });
        draft.GlobalPoolManagerApprovedAt = now.AddDays(-1);
        draft.GlobalPoolManagerApprovedByUserId = "e10";
        await db.SaveChangesAsync(ct);
    }
}

public sealed record PrimeEnrichmentCounts(
    int EnrichEmployees,
    int EnrichDrafts,
    int Fiches,
    int AuditLogs,
    int Anomalies,
    int Indicators);

public sealed record PrimeEnrichmentResult(
    bool Applied,
    string Reason,
    PrimeEnrichmentCounts? Before,
    PrimeEnrichmentCounts? After)
{
    public static PrimeEnrichmentResult Skipped(string reason) => new(false, reason, null, null);

    public static PrimeEnrichmentResult FromCounts(PrimeEnrichmentCounts before, PrimeEnrichmentCounts after) =>
        new(true, "ok", before, after);
}
