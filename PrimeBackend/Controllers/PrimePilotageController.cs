using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/pilotage")]
public sealed class PrimePilotageController(PrimeDbContext? db, PrimeInMemoryStore store) : ControllerBase
{
    private static string AggregateState(int total, int notStarted, int inProgress, int complete)
    {
        if (total == 0) return "Empty";
        if (notStarted == total) return "NotStarted";
        if (complete == total) return "Done";
        return "InProgress";
    }

    [HttpGet("cells-summary")]
    public async Task<ActionResult<List<CellPilotageSummaryDto>>> CellsSummary(
        [FromQuery] string supervisorUserId,
        [FromQuery] string period,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(period))
            return BadRequest(new { error = "supervisorUserId et period sont requis." });

        var poleIds = store.GetSupervisedPoleIds(supervisorUserId);
        if (poleIds.Count == 0) return Ok(new List<CellPilotageSummaryDto>());

        var per = period.Trim();
        var cells = new List<(string Id, string Name, string PoleId)>();
        foreach (var d in store.GetDepartments())
        {
            foreach (var p in d.Poles.Where(x => poleIds.Contains(x.Id)))
            {
                foreach (var c in p.Cells)
                    cells.Add((c.Id, c.Name, p.Id));
            }
        }

        var fiches = await db.EmployeePrimeCellFiches.AsNoTracking()
            .Where(f => f.Period == per && poleIds.Contains(f.PoleId))
            .ToListAsync(ct);

        var supTrim = supervisorUserId.Trim();
        var poleDrafts = await db.SupervisorPolePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && d.Period == per && poleIds.Contains(d.PoleId))
            .ToListAsync(ct);
        var linkedDraftByPole = new Dictionary<string, SupervisorPolePrimeDraftEntity>(StringComparer.Ordinal);
        foreach (var g in poleDrafts.GroupBy(d => d.PoleId, StringComparer.Ordinal))
            linkedDraftByPole[g.Key] = g.OrderByDescending(x => x.UpdatedAt).First();

        var result = new List<CellPilotageSummaryDto>();
        foreach (var (cellId, cellName, poleId) in cells)
        {
            var emps = store.GetEmployees()
                .Where(e => string.Equals(e.CelluleId, cellId, StringComparison.Ordinal))
                .ToList();
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

            linkedDraftByPole.TryGetValue(poleId, out var linkedDraft);
            result.Add(new CellPilotageSummaryDto
            {
                CelluleId = cellId,
                CelluleName = cellName,
                PoleId = poleId,
                TotalEmployees = total,
                NotStarted = notStarted,
                InProgress = inProgress,
                Complete = complete,
                CellAggregateState = AggregateState(total, notStarted, inProgress, complete),
                LinkedPolePrimeDraftId = linkedDraft?.Id,
                LinkedTemplateId = linkedDraft?.TemplateId,
                LinkedTemplateDisplayName = linkedDraft?.TemplateDisplayName,
            });
        }

        return Ok(result.OrderBy(r => r.CelluleName).ToList());
    }
}
