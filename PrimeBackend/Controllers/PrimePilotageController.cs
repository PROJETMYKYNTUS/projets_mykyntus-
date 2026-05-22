using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/pilotage")]
public sealed class PrimePilotageController(
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService? submission) : ControllerBase
{
    private static string AggregateState(int total, int notStarted, int inProgress, int complete)
    {
        if (total == 0) return "Empty";
        if (notStarted == total) return "NotStarted";
        if (complete == total) return "Done";
        return "InProgress";
    }

    private static SupervisorCellulePrimeDraftEntity? PickDraftForCellule(
        string celluleId,
        IReadOnlyList<SupervisorCellulePrimeDraftEntity> draftsForPeriod)
    {
        var forCell = draftsForPeriod
            .Where(d => string.Equals(d.CelluleId, celluleId, StringComparison.Ordinal))
            .OrderByDescending(d => d.UpdatedAt)
            .ToList();
        if (forCell.Count == 0) return null;
        return forCell.FirstOrDefault(d => PrimeFicheValidationSubmissionService.IsDraftValidated(d.Status))
               ?? forCell[0];
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

        var supTrim = supervisorUserId.Trim();
        var per = period.Trim();

        if (submission is not null)
            await submission.ReconcileReadySubmissionsForSupervisorPeriodAsync(supTrim, per, ct);

        var celluleIds = await org.GetSupervisedCelluleIdsAsync(supTrim, ct);
        if (celluleIds.Count == 0) return Ok(new List<ServicePilotageSummaryDto>());

        var cells = await org.GetServicesForCellulesAsync(celluleIds, ct);

        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.Period == per && celluleIds.Contains(f.CelluleId))
            .ToListAsync(ct);

        var poleDrafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && d.Period == per)
            .ToListAsync(ct);

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

            var cellDraft = PickDraftForCellule(celluleId, poleDrafts);

            var notStarted = 0;
            var inProgress = 0;
            var complete = 0;
            var readyCount = 0;
            var submittedForValidation = 0;
            foreach (var p in pilots)
            {
                var f = serviceFiches.FirstOrDefault(x => x.EmployeeId == p.Id);
                var st = f?.FillingStatus ?? "NotStarted";
                if (string.Equals(st, "Complete", StringComparison.OrdinalIgnoreCase)) complete++;
                else if (string.Equals(st, "InProgress", StringComparison.OrdinalIgnoreCase)) inProgress++;
                else notStarted++;

                if (f is null) continue;

                var isPending = string.Equals(
                    f.ValidationStatus,
                    PrimeValidationWorkflowService.Pending,
                    StringComparison.Ordinal);
                if (isPending)
                {
                    submittedForValidation++;
                    continue;
                }

                var ready = false;
                if (submission is not null)
                    ready = await submission.ComputeIsReadyForValidationAsync(f, ct);
                else if (cellDraft is not null)
                    ready = PrimeFicheValidationSubmissionService.ComputeIsReadyForValidation(cellDraft, f);

                if (ready)
                    readyCount++;
            }

            var readyForValidation = readyCount + submittedForValidation;

            var poolOk = cellDraft is not null && cellDraft.GlobalPoolManagerApprovedAt.HasValue &&
                         cellDraft.GlobalPoolRhApprovedAt.HasValue;

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
                ReadyCount = readyCount,
                SubmittedForValidationCount = submittedForValidation,
                ReadyForValidation = readyForValidation,
                CommonPartStatus = cellDraft?.Status,
                ServiceAggregateState = AggregateState(total, notStarted, inProgress, complete),
                LinkedCellulePrimeDraftId = cellDraft?.Id,
                LinkedTemplateId = cellDraft?.TemplateId,
                LinkedTemplateDisplayName = cellDraft?.TemplateDisplayName,
                PoolDistributionUnlocked = poolOk,
            });
        }

        return Ok(result
            .OrderBy(r => r.CelluleName)
            .ThenBy(r => r.ServiceName)
            .ToList());
    }
}
