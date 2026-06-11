using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/services/{serviceId}/prime-indicators")]
public sealed class ServicePrimeIndicatorsController(
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    PrimeDeletionGuardService? deletionGuard) : ControllerBase
{
    private static ServicePrimeIndicatorDto Map(ServicePrimeIndicatorEntity e) =>
        new()
        {
            Id = e.Id,
            ServiceId = e.ServiceId,
            SortOrder = e.SortOrder,
            Label = e.Label,
            PonderationPrimePct = e.PonderationPrimePct,
            PonderationChallengePct = e.PonderationChallengePct,
            IsActive = e.IsActive,
            TemplateStableId = e.TemplateStableId,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };

    [HttpGet]
    public async Task<ActionResult<List<ServicePrimeIndicatorDto>>> Get(
        string serviceId,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId)) return BadRequest(new { error = "supervisorUserId requis." });

        var celluleId = await org.GetCelluleIdForServiceAsync(serviceId, ct);
        if (celluleId is null) return NotFound(new { error = "Cellule introuvable." });
        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, celluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var list = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(x => x.ServiceId == serviceId.Trim())
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return Ok(list.ConvertAll(Map));
    }

    [HttpPut]
    public async Task<ActionResult<List<ServicePrimeIndicatorDto>>> Put(
        string serviceId,
        [FromQuery] string supervisorUserId,
        [FromBody] PutServicePrimeIndicatorsRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId)) return BadRequest(new { error = "supervisorUserId requis." });

        var celluleId = await org.GetCelluleIdForServiceAsync(serviceId, ct);
        if (celluleId is null) return NotFound(new { error = "Cellule introuvable." });
        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, celluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        if (deletionGuard is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        var cid = serviceId.Trim();
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ServicePrimeIndicators.Where(x => x.ServiceId == cid).ToListAsync(ct);

        static bool MatchesIncoming(ServicePrimeIndicatorEntity old, PutServicePrimeIndicatorItem item)
        {
            var stable = (old.TemplateStableId ?? "").Trim();
            var incomingStable = (item.TemplateStableId ?? "").Trim();
            if (stable.Length > 0 && incomingStable.Length > 0 &&
                string.Equals(stable, incomingStable, StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(old.Label.Trim(), item.Label.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        var retainedInactive = new List<ServicePrimeIndicatorEntity>();
        foreach (var old in existing)
        {
            if (body.Indicators.Any(i => MatchesIncoming(old, i))) continue;
            if (await deletionGuard.IsIndicatorProtectedByFichesAsync(old, cid, ct))
            {
                retainedInactive.Add(new ServicePrimeIndicatorEntity
                {
                    Id = old.Id,
                    ServiceId = old.ServiceId,
                    SortOrder = old.SortOrder,
                    Label = old.Label,
                    PonderationPrimePct = old.PonderationPrimePct,
                    PonderationChallengePct = old.PonderationChallengePct,
                    IsActive = false,
                    TemplateStableId = old.TemplateStableId,
                    CreatedAt = old.CreatedAt,
                    UpdatedAt = now,
                });
            }
        }

        db.ServicePrimeIndicators.RemoveRange(existing);

        foreach (var item in body.Indicators.OrderBy(i => i.SortOrder))
        {
            db.ServicePrimeIndicators.Add(new ServicePrimeIndicatorEntity
            {
                Id = Guid.NewGuid(),
                ServiceId = cid,
                SortOrder = item.SortOrder,
                Label = item.Label.Trim(),
                PonderationPrimePct = item.PonderationPrimePct,
                PonderationChallengePct = item.PonderationChallengePct,
                IsActive = item.IsActive,
                TemplateStableId = string.IsNullOrWhiteSpace(item.TemplateStableId) ? null : item.TemplateStableId.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        foreach (var kept in retainedInactive)
            db.ServicePrimeIndicators.Add(kept);

        await db.SaveChangesAsync(ct);
        var list = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(x => x.ServiceId == cid)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return Ok(list.ConvertAll(Map));
    }
}
