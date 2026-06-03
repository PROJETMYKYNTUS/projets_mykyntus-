using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Services;

public sealed class PrimeGlobalSynthesisReadinessService(
    PrimeDbContext db,
    PrimeValidationWorkflowRuntime wfRuntime)
{
    public async Task<GlobalPoolReadinessDto> GetReadinessAsync(string period, CancellationToken ct = default)
    {
        var per = period.Trim();
        var terminals = (await wfRuntime.GetTerminalStatusesAsync(ct))
            .Where(s => !string.Equals(s, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.Period == per)
            .ToListAsync(ct);

        var services = await db.Services.AsNoTracking()
            .Include(s => s.Cellule)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.ServiceId != null)
            .ToListAsync(ct);

        var serviceReadiness = new List<GlobalPoolServiceReadinessDto>();
        foreach (var srv in services)
        {
            var (ready, total, validated, reason) = EvaluateService(srv.Id, fiches, employees, terminals);
            serviceReadiness.Add(new GlobalPoolServiceReadinessDto
            {
                ServiceId = srv.Id,
                ServiceName = srv.Name,
                CelluleId = srv.CelluleId,
                PoleId = srv.Cellule?.PoleId ?? "",
                Ready = ready,
                FichesTotal = total,
                FichesValidated = validated,
                BlockingReason = reason,
            });
        }

        var byCellule = services.GroupBy(s => s.CelluleId, StringComparer.Ordinal).ToList();
        var celluleReadiness = new List<GlobalPoolCelluleReadinessDto>();
        foreach (var g in byCellule)
        {
            var cell = g.First().Cellule;
            var svcInCell = serviceReadiness.Where(s => s.CelluleId == g.Key).ToList();
            var readyCount = svcInCell.Count(s => s.Ready);
            celluleReadiness.Add(new GlobalPoolCelluleReadinessDto
            {
                CelluleId = g.Key,
                CelluleName = cell?.Name ?? g.Key,
                PoleId = cell?.PoleId ?? "",
                Ready = svcInCell.Count > 0 && readyCount == svcInCell.Count,
                ServicesReady = readyCount,
                ServicesTotal = svcInCell.Count,
                BlockingReason = readyCount < svcInCell.Count
                    ? $"{svcInCell.Count - readyCount} service(s) non prêt(s)"
                    : null,
            });
        }

        var byPole = celluleReadiness.GroupBy(c => c.PoleId, StringComparer.Ordinal).ToList();
        var poles = await db.Poles.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var poleReadiness = poles.Select(p =>
        {
            var cells = celluleReadiness.Where(c => c.PoleId == p.Id).ToList();
            var readyCells = cells.Count(c => c.Ready);
            return new GlobalPoolPoleReadinessDto
            {
                PoleId = p.Id,
                PoleName = p.Name,
                Ready = cells.Count > 0 && readyCells == cells.Count,
                CellulesReady = readyCells,
                CellulesTotal = cells.Count,
                BlockingReason = readyCells < cells.Count
                    ? $"{cells.Count - readyCells} cellule(s) non prête(s)"
                    : null,
            };
        }).ToList();

        return new GlobalPoolReadinessDto
        {
            Period = per,
            Services = serviceReadiness,
            Cellules = celluleReadiness,
            Poles = poleReadiness,
        };
    }

    public async Task<bool> IsScopeReadyAsync(string period, string scopeType, string scopeId, CancellationToken ct = default)
    {
        var dto = await GetReadinessAsync(period, ct);
        return scopeType switch
        {
            _ when string.Equals(scopeType, GlobalPoolScopeTypes.Service, StringComparison.Ordinal) =>
                dto.Services.FirstOrDefault(s => s.ServiceId == scopeId)?.Ready ?? false,
            _ when string.Equals(scopeType, GlobalPoolScopeTypes.Cellule, StringComparison.Ordinal) =>
                dto.Cellules.FirstOrDefault(c => c.CelluleId == scopeId)?.Ready ?? false,
            _ when string.Equals(scopeType, GlobalPoolScopeTypes.Pole, StringComparison.Ordinal) =>
                dto.Poles.FirstOrDefault(p => p.PoleId == scopeId)?.Ready ?? false,
            _ => false,
        };
    }

    private static (bool ready, int total, int validated, string? reason) EvaluateService(
        string serviceId,
        List<EmployeePrimeServiceFicheEntity> allFiches,
        List<EmployeeEntity> employees,
        HashSet<string> terminalStatuses)
    {
        // Périmètre attendu = pilotes ACTUELLEMENT rattachés au service (source DB).
        // On n'ajoute plus les propriétaires de fiches : une fiche d'un pilote retiré ne doit
        // plus compter ni gonfler le total (l'interface doit refléter l'état courant).
        var expectedIds = employees
            .Where(e => string.Equals(e.ServiceId, serviceId, StringComparison.Ordinal)
                && string.Equals(e.Role, "Pilote", StringComparison.Ordinal))
            .Select(e => e.Id)
            .ToHashSet(StringComparer.Ordinal);

        var fiches = allFiches
            .Where(f => string.Equals(f.ServiceId, serviceId, StringComparison.Ordinal)
                && expectedIds.Contains(f.EmployeeId))
            .ToList();

        if (expectedIds.Count == 0)
            return (true, 0, 0, null);

        var validated = 0;
        string? firstReason = null;
        foreach (var empId in expectedIds)
        {
            var fiche = fiches.FirstOrDefault(f => f.EmployeeId == empId);
            if (fiche is null)
            {
                firstReason ??= $"Fiche manquante pour l'employé {empId}";
                continue;
            }

            if (string.Equals(fiche.ValidationStatus, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            {
                firstReason ??= $"Fiche rejetée ({empId})";
                continue;
            }

            if (!string.Equals(fiche.FillingStatus, "Complete", StringComparison.OrdinalIgnoreCase))
            {
                firstReason ??= $"Saisie incomplète ({empId})";
                continue;
            }

            if (!terminalStatuses.Contains(fiche.ValidationStatus))
            {
                firstReason ??= $"Validation en cours : {fiche.ValidationStatus} ({empId})";
                continue;
            }

            validated++;
        }

        var ready = expectedIds.Count > 0 && validated == expectedIds.Count;
        return (ready, expectedIds.Count, validated, ready ? null : firstReason);
    }
}
