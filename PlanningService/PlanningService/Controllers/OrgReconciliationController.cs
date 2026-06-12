using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/admin/org-reconciliation")]
public sealed class OrgReconciliationController(AppDbContext db, ILogger<OrgReconciliationController> logger) : ControllerBase
{
    /// <summary>Rapproche les IDs Prime manquants sur les nœuds Planning (par nom + niveau).</summary>
    [HttpPost("backfill-from-prime")]
    public async Task<IActionResult> BackfillFromPrime([FromBody] PrimeOrgBackfillRequest request, CancellationToken ct)
    {
        var report = new List<string>();

        foreach (var pole in request.Poles)
        {
            var floor = await db.Floors.FirstOrDefaultAsync(f => f.PrimePoleId == pole.Id || f.Name == pole.Name, ct);
            if (floor is null)
            {
                floor = new Models.Floor
                {
                    Name = pole.Name,
                    FloorNumber = await db.Floors.CountAsync(ct) + 1,
                    PrimePoleId = pole.Id
                };
                db.Floors.Add(floor);
                report.Add($"Created Floor for pole {pole.Name}");
            }
            else if (floor.PrimePoleId != pole.Id)
            {
                floor.PrimePoleId = pole.Id;
                report.Add($"Linked Floor {floor.Name} → {pole.Id}");
            }

            await db.SaveChangesAsync(ct);

            foreach (var cellule in pole.Cellules)
            {
                var service = await db.Services.FirstOrDefaultAsync(
                    s => s.PrimeCelluleId == cellule.Id || (s.FloorId == floor.Id && s.Name == cellule.Name), ct);
                if (service is null)
                {
                    service = new Service
                    {
                        FloorId = floor.Id,
                        Name = cellule.Name,
                        Code = $"CELL-{cellule.Id}",
                        PrimeCelluleId = cellule.Id
                    };
                    db.Services.Add(service);
                    report.Add($"Created Service (cellule) {cellule.Name}");
                }
                else
                {
                    service.PrimeCelluleId = cellule.Id;
                    service.FloorId = floor.Id;
                }

                await db.SaveChangesAsync(ct);

                foreach (var svc in cellule.Services)
                {
                    var sub = await db.SubServices.FirstOrDefaultAsync(
                        s => s.PrimeServiceId == svc.Id || (s.ServiceId == service.Id && s.Name == svc.Name), ct);
                    if (sub is null)
                    {
                        sub = new Models.SubService
                        {
                            ServiceId = service.Id,
                            Name = svc.Name,
                            Code = $"SVC-{svc.Id}",
                            PrimeServiceId = svc.Id
                        };
                        db.SubServices.Add(sub);
                        report.Add($"Created SubService {svc.Name}");
                    }
                    else
                    {
                        sub.PrimeServiceId = svc.Id;
                        sub.ServiceId = service.Id;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Org reconciliation: {Count} actions", report.Count);
        return Ok(new { actions = report, count = report.Count });
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct)
    {
        var floorsWithoutPrime = await db.Floors.CountAsync(f => f.PrimePoleId == null, ct);
        var servicesWithoutPrime = await db.Services.CountAsync(s => s.PrimeCelluleId == null, ct);
        var subsWithoutPrime = await db.SubServices.CountAsync(s => s.PrimeServiceId == null, ct);
        var users = await db.Users.CountAsync(u => u.IsActive, ct);

        return Ok(new
        {
            floorsWithoutPrimeId = floorsWithoutPrime,
            servicesWithoutPrimeCelluleId = servicesWithoutPrime,
            subServicesWithoutPrimeServiceId = subsWithoutPrime,
            activeUsers = users,
            ok = subsWithoutPrime == 0
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
