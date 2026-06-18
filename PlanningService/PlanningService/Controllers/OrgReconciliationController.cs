using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Services;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/admin/org-reconciliation")]
[Authorize(Roles = "Admin,RH,Manager,Superviseur,Coach,Pilote")]
public sealed class OrgReconciliationController(
    AppDbContext db,
    IPlanningOrgMirrorService mirror,
    ILogger<OrgReconciliationController> logger) : ControllerBase
{
    /// <summary>Rapproche les IDs Prime manquants sur les nœuds Planning (par nom + niveau).</summary>
    [HttpPost("backfill-from-prime")]
    public async Task<IActionResult> BackfillFromPrime([FromBody] PrimeOrgBackfillRequest request, CancellationToken ct)
    {
        var poles = request.Poles.Select(p => new PrimeOrgPoleMirrorDto
        {
            Id = p.Id,
            Name = p.Name,
            Cellules = p.Cellules.Select(c => new PrimeOrgCelluleMirrorDto
            {
                Id = c.Id,
                Name = c.Name,
                Services = c.Services.Select(s => new PrimeOrgLeafServiceMirrorDto { Id = s.Id, Name = s.Name }).ToList(),
            }).ToList(),
        }).ToList();

        var count = await mirror.SyncFromPrimeTreeAsync(poles, ct);
        logger.LogInformation("Org reconciliation manual: {Count} actions", count);
        return Ok(new { count });
    }

    /// <summary>Pull Organisation RH (Prime) et synchronise le miroir Planning — sans body.</summary>
    [HttpPost("sync-from-prime")]
    public async Task<IActionResult> SyncFromPrime(CancellationToken ct)
    {
        await PlanningOrgBootstrap.SyncFromPrimeAsync(HttpContext.RequestServices, ct);
        var verify = await Verify(ct);
        return verify;
    }

    /// <summary>Aligne le miroir Planning depuis Employee Directory (source canonique).</summary>
    [HttpPost("sync-from-directory")]
    public async Task<IActionResult> SyncFromDirectory(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.ToString();
        var actions = await mirror.SyncFromDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);
        logger.LogInformation("Org mirror sync-from-directory: {Count} action(s)", actions);
        return await Verify(ct);
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct)
    {
        var floorsWithoutPrime = await db.Floors.CountAsync(f => f.PrimePoleId == null, ct);
        var servicesWithoutPrime = await db.Services.CountAsync(s => s.PrimeCelluleId == null, ct);
        var subsWithoutPrime = await db.SubServices.CountAsync(s => s.PrimeServiceId == null, ct);
        var users = await db.Users.CountAsync(u => u.IsActive, ct);

        var duplicateSubNames = await db.SubServices
            .GroupBy(s => new { s.ServiceId, s.Name })
            .Where(g => g.Count() > 1)
            .CountAsync(ct);

        return Ok(new
        {
            floorsWithoutPrimeId = floorsWithoutPrime,
            servicesWithoutPrimeCelluleId = servicesWithoutPrime,
            subServicesWithoutPrimeServiceId = subsWithoutPrime,
            duplicateSubServiceNames = duplicateSubNames,
            activeUsers = users,
            ok = subsWithoutPrime == 0 && floorsWithoutPrime == 0 && servicesWithoutPrime == 0,
        });
    }
}

public sealed class PrimeOrgBackfillRequest
{
    public List<PrimePoleBackfillDto> Poles { get; init; } = [];
}

public sealed class PrimePoleBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeCelluleBackfillDto> Cellules { get; init; } = [];
}

public sealed class PrimeCelluleBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeServiceBackfillDto> Services { get; init; } = [];
}

public sealed class PrimeServiceBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}
