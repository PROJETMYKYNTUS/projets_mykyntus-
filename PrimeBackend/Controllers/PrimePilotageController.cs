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
        var linkedDraftByPole = new Dictionary<string, SupervisorCellulePrimeDraftEntity>(StringComparer.Ordinal);
        foreach (var g in poleDrafts.GroupBy(d => d.CelluleId, StringComparer.Ordinal))
            linkedDraftByPole[g.Key] = g.OrderByDescending(x => x.UpdatedAt).First();

        var result = new List<ServicePilotageSummaryDto>();
        foreach (var (cellId, cellName, celluleId) in cells)
        {
            var emps = await org.GetEmployeesInServiceAsync(cellId, ct);
            var total = emps.Count;
            var empIds = emps.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            var cellFiches = fiches.Where(f => empIds.Contains(f.EmployeeId)).ToList();

            var notStarted = 0;
            var inProgress = 0;
            var complete = 0;
            foreach (var e in emps)
            {
                var f = cellFiches.FirstOrDefault(x => x.EmployeeId == e.Id);
                var st = f?.FillingStatus ?? "NotStarted";
                if (string.Equals(st, "Complete", StringComparison.OrdinalIgnoreCase)) complete++;
                else if (string.Equals(st, "InProgress", StringComparison.OrdinalIgnoreCase)) inProgress++;
                else notStarted++;
            }

            linkedDraftByPole.TryGetValue(celluleId, out var linkedDraft);
            var poolOk = linkedDraft is not null && linkedDraft.GlobalPoolManagerApprovedAt.HasValue &&
                         linkedDraft.GlobalPoolRhApprovedAt.HasValue;
            result.Add(new ServicePilotageSummaryDto
            {
                ServiceId = cellId,
                ServiceName = cellName,
                CelluleId = celluleId,
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

        return Ok(result.OrderBy(r => r.ServiceName).ToList());
    }
}
