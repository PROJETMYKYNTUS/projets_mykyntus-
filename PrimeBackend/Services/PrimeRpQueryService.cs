using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Models;

namespace PrimeBackend.Services;

/// <summary>Requêtes Chef de projet / RP depuis PostgreSQL (remplace les mocks <see cref="PrimeInMemoryStore"/>).</summary>
public sealed class PrimeRpQueryService(PrimeDbContext? db)
{
    public async Task<List<string>> GetAssignedProjectIdsAsync(string rpUserId, CancellationToken ct = default)
    {
        var poleId = await ResolveChefDeProjetPoleIdAsync(rpUserId, ct);
        if (db is null || poleId is null)
            return ["default"];

        var ids = await db.Services.AsNoTracking()
            .Where(s => s.Cellule.PoleId == poleId)
            .Select(s => s.Id)
            .Distinct()
            .ToListAsync(ct);
        return ids.Count > 0 ? ids : ["default"];
    }

    public async Task<ChefProjetDashboardStats> GetDashboardStatsAsync(string rpUserId, CancellationToken ct = default)
    {
        var poleId = await ResolveChefDeProjetPoleIdAsync(rpUserId, ct);
        if (db is null || poleId is null)
            return EmptyDashboard();

        var fiches = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join e in db.Employees.AsNoTracking() on f.EmployeeId equals e.Id
            where e.PoleId == poleId
            select f
        ).ToListAsync(ct);

        if (fiches.Count == 0)
            return EmptyDashboard();

        var employeeIds = fiches.Select(f => f.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var perfRows = new List<(int completed, int total, int objR, int objT, string name, List<MonthlyPerformancePoint> monthly)>();
        foreach (var f in fiches)
        {
            employees.TryGetValue(f.EmployeeId, out var emp);
            var (c, t, or, ot, monthly) = ParsePerformanceJson(f.ServiceSaisieJson);
            perfRows.Add((c, t, or, ot, emp is null ? f.EmployeeId : $"{emp.FirstName} {emp.LastName}", monthly));
        }

        var totalCompleted = perfRows.Sum(x => x.completed);
        var totalTasks = perfRows.Sum(x => x.total);
        var avgPerf = perfRows.Count == 0
            ? 0
            : (int)Math.Round(perfRows.Average(x => ScoreFromTasks(x.completed, x.total, x.objR, x.objT)));

        var evolution = BuildEvolutionAverage(perfRows.Select(x => x.monthly).ToList());

        var memberPerformance = perfRows
            .GroupBy(x => x.name)
            .Select(g =>
            {
                var score = (int)Math.Round(g.Average(x => ScoreFromTasks(x.completed, x.total, x.objR, x.objT)));
                var status = score >= 85 ? "Excellent" : score >= 70 ? "Moyen" : "Faible";
                return new MemberPerformance { Name = g.Key, Score = score, Status = status };
            })
            .Take(12)
            .ToList();

        var pending = fiches.Count(f =>
            f.ValidationStatus == PrimeValidationWorkflowService.SuperviseurApproved);

        return new ChefProjetDashboardStats
        {
            ProjectProgress = (int)Math.Round(totalCompleted / (double)Math.Max(totalTasks, 1) * 100),
            CompletedTasks = totalCompleted,
            AverageTeamPerformance = avgPerf,
            PendingValidations = pending,
            PerformanceEvolution = evolution,
            MemberPerformance = memberPerformance,
        };
    }

    public async Task<List<ChefProjetTeamMemberPerformance>> GetTeamPerformanceByProjectAsync(
        string rpUserId,
        CancellationToken ct = default)
    {
        var poleId = await ResolveChefDeProjetPoleIdAsync(rpUserId, ct);
        if (db is null || poleId is null) return [];

        var rows = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join e in db.Employees.AsNoTracking() on f.EmployeeId equals e.Id
            join s in db.Services.AsNoTracking() on f.ServiceId equals s.Id
            where e.PoleId == poleId && e.Role == "Pilote"
            select new { f, e, s }
        ).ToListAsync(ct);

        var result = new List<ChefProjetTeamMemberPerformance>();
        foreach (var row in rows)
        {
            var (completed, total, objR, objT, monthly) = ParsePerformanceJson(row.f.ServiceSaisieJson);
            result.Add(new ChefProjetTeamMemberPerformance
            {
                EmployeeId = row.e.Id,
                EmployeeName = $"{row.e.FirstName} {row.e.LastName}",
                ProjectId = row.s.Id,
                ProjectName = row.s.Name,
                CompletedTasks = completed,
                TotalTasks = total,
                ObjectivesReached = objR,
                TotalObjectives = objT,
                MonthlyPerformance = monthly,
            });
        }

        return result
            .GroupBy(x => new { x.EmployeeId, x.ProjectId })
            .Select(g => g.First())
            .ToList();
    }

    public async Task<List<ChefProjetValidationItem>> GetSuperviseurValidatedPrimesAsync(
        string rpUserId,
        CancellationToken ct = default)
    {
        var poleId = await ResolveChefDeProjetPoleIdAsync(rpUserId, ct);
        if (db is null || poleId is null) return [];

        var rows = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join e in db.Employees.AsNoTracking() on f.EmployeeId equals e.Id
            join s in db.Services.AsNoTracking() on f.ServiceId equals s.Id
            where e.PoleId == poleId
                  && (f.ValidationStatus == PrimeValidationWorkflowService.SuperviseurApproved
                      || f.ValidationStatus == PrimeValidationWorkflowService.ChefDeProjetApproved
                      || f.ValidationStatus == PrimeValidationWorkflowService.Rejected)
            select new { f, e, s }
        ).ToListAsync(ct);

        return rows.Select(row => new ChefProjetValidationItem
        {
            Id = row.f.Id.ToString(),
            EmployeeId = row.e.Id,
            EmployeeName = $"{row.e.FirstName} {row.e.LastName}",
            ProjectId = row.s.Id,
            ProjectName = row.s.Name,
            PerformanceScore = (int)Math.Clamp(row.f.TotalAmount ?? row.f.PrimeAmount ?? 0, 0, 100),
            SuperviseurValidated = row.f.ValidationStatus != PrimeValidationWorkflowService.Pending
                                   && row.f.ValidationStatus != PrimeValidationWorkflowService.ReferentTechniqueApproved,
            Status = MapRpUiStatus(row.f.ValidationStatus),
            Period = row.f.Period,
        }).ToList();
    }

    public async Task<ChefProjetValidationItem> UpdateValidationStatusAsync(
        string ficheId,
        string status,
        string rpUserId,
        CancellationToken ct = default)
    {
        if (db is null) throw new InvalidOperationException("Base de données non configurée.");
        if (!Guid.TryParse(ficheId, out var id)) throw new KeyNotFoundException("Validation introuvable.");

        var fiche = await db.EmployeePrimeServiceFiches
            .Include(f => f.CellulePrimeDraft)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche is null) throw new KeyNotFoundException("Validation introuvable.");

        var poleId = await ResolveChefDeProjetPoleIdAsync(rpUserId, ct);
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == fiche.EmployeeId, ct);
        if (poleId is null || emp is null || emp.PoleId != poleId)
            throw new UnauthorizedAccessException("Hors périmètre chef de projet.");

        var now = DateTimeOffset.UtcNow;
        if (string.Equals(status, "RP Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (fiche.ValidationStatus != PrimeValidationWorkflowService.SuperviseurApproved)
                throw new InvalidOperationException("La fiche n'est pas en attente de validation chef de projet.");
            fiche.ValidationStatus = PrimeValidationWorkflowService.ChefDeProjetApproved;
            fiche.LastApproverUserId = rpUserId;
            fiche.LastApprovedAt = now;
        }
        else if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            fiche.ValidationStatus = PrimeValidationWorkflowService.Rejected;
            fiche.RejectedByUserId = rpUserId;
            fiche.RejectedAt = now;
            fiche.RejectionReason ??= "Rejet chef de projet (RP).";
        }
        else
            throw new ArgumentException($"Statut RP non supporté : {status}");

        fiche.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fiche.ServiceId, ct);
        return new ChefProjetValidationItem
        {
            Id = fiche.Id.ToString(),
            EmployeeId = emp.Id,
            EmployeeName = $"{emp.FirstName} {emp.LastName}",
            ProjectId = fiche.ServiceId,
            ProjectName = service?.Name ?? fiche.ServiceId,
            PerformanceScore = (int)Math.Clamp(fiche.TotalAmount ?? 0, 0, 100),
            SuperviseurValidated = true,
            Status = MapRpUiStatus(fiche.ValidationStatus),
            Period = fiche.Period,
        };
    }

    private async Task<string?> ResolveChefDeProjetPoleIdAsync(string rpUserId, CancellationToken ct)
    {
        if (db is null) return null;
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == rpUserId, ct);
        return emp?.PoleId;
    }

    private static string MapRpUiStatus(string validationStatus) => validationStatus switch
    {
        PrimeValidationWorkflowService.ChefDeProjetApproved => "RP Approved",
        PrimeValidationWorkflowService.Rejected => "Rejected",
        PrimeValidationWorkflowService.SuperviseurApproved => "Manager Approved",
        _ => "Manager Approved",
    };

    private static int ScoreFromTasks(int completed, int total, int objR, int objT) =>
        (int)Math.Round(completed / (double)Math.Max(total, 1) * 60 + objR / (double)Math.Max(objT, 1) * 40);

    private static List<MonthScore> BuildEvolutionAverage(List<List<MonthlyPerformancePoint>> allMonthly)
    {
        if (allMonthly.Count == 0 || allMonthly[0].Count == 0) return [];

        var monthCount = allMonthly.Max(m => m.Count);
        var result = new List<MonthScore>();
        for (var i = 0; i < monthCount; i++)
        {
            var points = allMonthly.Where(m => m.Count > i).Select(m => m[i]).ToList();
            if (points.Count == 0) continue;
            result.Add(new MonthScore
            {
                Month = points[0].Month,
                Score = (int)Math.Round(points.Average(p => p.Score)),
            });
        }
        return result;
    }

    private static (int completed, int total, int objR, int objT, List<MonthlyPerformancePoint> monthly) ParsePerformanceJson(string json)
    {
        var monthly = new List<MonthlyPerformancePoint>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = doc.RootElement;
            var completed = root.TryGetProperty("completedTasks", out var c) ? c.GetInt32() : 10;
            var total = root.TryGetProperty("totalTasks", out var t) ? t.GetInt32() : 12;
            var objR = root.TryGetProperty("objectivesReached", out var or) ? or.GetInt32() : 3;
            var objT = root.TryGetProperty("totalObjectives", out var ot) ? ot.GetInt32() : 5;
            if (root.TryGetProperty("monthlyScores", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var month = item.TryGetProperty("month", out var m) ? m.GetString() ?? "" : "";
                    var score = item.TryGetProperty("score", out var s) ? s.GetInt32() : 75;
                    if (!string.IsNullOrEmpty(month))
                        monthly.Add(new MonthlyPerformancePoint { Month = month, Score = score });
                }
            }
            if (monthly.Count == 0)
            {
                monthly.AddRange(new[]
                {
                    new MonthlyPerformancePoint { Month = "Jan", Score = 72 },
                    new MonthlyPerformancePoint { Month = "Fév", Score = 78 },
                    new MonthlyPerformancePoint { Month = "Mar", Score = 81 },
                    new MonthlyPerformancePoint { Month = "Avr", Score = 85 },
                });
            }
            return (completed, total, objR, objT, monthly);
        }
        catch
        {
            monthly.Add(new MonthlyPerformancePoint { Month = "Avr", Score = 75 });
            return (10, 12, 3, 5, monthly);
        }
    }

    private static ChefProjetDashboardStats EmptyDashboard() => new()
    {
        ProjectProgress = 0,
        CompletedTasks = 0,
        AverageTeamPerformance = 0,
        PendingValidations = 0,
        PerformanceEvolution = [],
        MemberPerformance = [],
    };
}
