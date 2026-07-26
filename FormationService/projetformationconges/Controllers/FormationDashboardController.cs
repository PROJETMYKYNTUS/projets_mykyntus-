using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/dashboard")]
public sealed class FormationDashboardController(TrainingWorkflowService training) : ControllerBase
{
    [HttpGet("stats")]
    [Authorize(Policy = "CanPlanContinue")]
    public Task<FormationDashboardStatsDto> Stats(
        [FromQuery] Guid[]? employeeIds,
        CancellationToken ct) =>
        training.GetDashboardStatsAsync(employeeIds, ct);

    [HttpGet("stats-initial")]
    [Authorize(Policy = "CanPlanContinue")]
    public Task<FormationInitialDashboardStatsDto> StatsInitial(
        [FromQuery] Guid[]? employeeIds,
        CancellationToken ct) =>
        training.GetInitialDashboardStatsAsync(employeeIds, ct);
}
