using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/pilotage")]
public sealed class PrimePilotageController(PrimeDbContext? db, PrimeOrgScopeService org) : ControllerBase
{
    private static string AggregateState(int total, int notStarted, int inProgress, int complete)
    {
        if (total == 0) return "Empty";
        if (notStarted == total) return "NotStarted";
        if (complete == total) return "Done";
        return "InProgress";
    }

    [HttpGet("cells-summary")]
    public async Task<ActionResult<List<ServicePilotageSummaryDto>>> CellsSummary(
        [FromQuery] string supervisorUserId,
        [FromQuery] string period,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(period))
            return BadRequest(new { error = "supervisorUserId et period sont requis." });

        var celluleIds = await org.GetSupervisedCelluleIdsAsync(supervisorUserId, ct);
        if (celluleIds.Count == 0) return Ok(new List<ServicePilotageSummaryDto>());

        var per = period.Trim();
        var cells = await org.GetServicesForCellulesAsync(celluleIds, ct);

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.Period == per && celluleIds.Contains(f.CelluleId))
            .ToListAsync(ct);

        var supTrim = supervisorUserId.Trim();
        var poleDrafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && d.Period == per && celluleIds.Contains(d.CelluleId))
            .ToListAsync(ct);
        var linkedDraftByCellule = new Dictionary<string, SupervisorCellulePrimeDraftEntity>(StringComparer.Ordinal);
        foreach (var g in poleDrafts.GroupBy(d => d.CelluleId, StringComparer.Ordinal))
            linkedDraftByCellule[g.Key] = g.OrderByDescending(x => x.UpdatedAt).First();

        var distinctCelluleIds = cells.Select(c => c.CelluleId).Distinct(StringComparer.Ordinal).ToList();
        var celluleEntities = await db.Cellules.AsNoTracking()
            .Where(c => distinctCelluleIds.Contains(c.Id))
            .ToListAsync(ct);
        var cellulesById = celluleEntities.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var poleIds = celluleEntities.Select(c => c.PoleId).Distinct(StringComparer.Ordinal).ToList();
        var polesById = poleIds.Count == 0
            ? new Dictionary<string, PoleEntity>(StringComparer.Ordinal)
            : await db.Poles.AsNoTracking()
                .Where(p => poleIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, StringComparer.Ordinal, ct);

        var result = new List<ServicePilotageSummaryDto>();
        foreach (var (serviceId, serviceName, celluleId) in cells)
        {
            var pilots = await org.GetPilotsInServiceAsync(serviceId, ct);
            var total = pilots.Count;
            var pilotIds = pilots.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            var serviceFiches = fiches.Where(f => pilotIds.Contains(f.EmployeeId)).ToList();

            var notStarted = 0;
            var inProgress = 0;
            var complete = 0;
            foreach (var p in pilots)
            {
                var f = serviceFiches.FirstOrDefault(x => x.EmployeeId == p.Id);
                var st = f?.FillingStatus ?? "NotStarted";
                if (string.Equals(st, "Complete", StringComparison.OrdinalIgnoreCase)) complete++;
                else if (string.Equals(st, "InProgress", StringComparison.OrdinalIgnoreCase)) inProgress++;
                else notStarted++;
            }

            linkedDraftByCellule.TryGetValue(celluleId, out var linkedDraft);
            var poolOk = linkedDraft is not null && linkedDraft.GlobalPoolManagerApprovedAt.HasValue &&
                         linkedDraft.GlobalPoolRhApprovedAt.HasValue;

            cellulesById.TryGetValue(celluleId, out var cellEnt);
            var poleName = cellEnt is not null && polesById.TryGetValue(cellEnt.PoleId, out var pole)
                ? pole.Name
                : "";

            result.Add(new ServicePilotageSummaryDto
            {
                ServiceId = serviceId,
                ServiceName = serviceName,
                CelluleId = celluleId,
                CelluleName = cellEnt?.Name ?? celluleId,
                PoleName = poleName,
                TotalEmployees = total,
                NotStarted = notStarted,
                InProgress = inProgress,
                Complete = complete,
                ServiceAggregateState = AggregateState(total, notStarted, inProgress, complete),
                LinkedCellulePrimeDraftId = linkedDraft?.Id,
                LinkedTemplateId = linkedDraft?.TemplateId,
                LinkedTemplateDisplayName = linkedDraft?.TemplateDisplayName,
                PoolDistributionUnlocked = poolOk,
            });
        }

        return Ok(result
            .OrderBy(r => r.CelluleName)
            .ThenBy(r => r.ServiceName)
            .ToList());
    }
}
