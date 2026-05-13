using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/supervisor-pole-prime-drafts")]
public sealed class SupervisorPolePrimeDraftController(PrimeDbContext? db, PrimeInMemoryStore store) : ControllerBase
{
    private static SupervisorPolePrimeDraftResponseDto Map(SupervisorPolePrimeDraftEntity e) =>
        new()
        {
            Id = e.Id,
            SupervisorUserId = e.SupervisorUserId,
            PoleId = e.PoleId,
            Period = e.Period,
            TemplateId = e.TemplateId,
            TemplateDisplayName = e.TemplateDisplayName,
            TemplateFormatVersion = e.TemplateFormatVersion,
            Status = e.Status,
            SchemaJson = e.SchemaJson,
            PoleSaisieJson = e.PoleSaisieJson,
            ComputedJson = e.ComputedJson,
            TemplateCalcSnapshotJson = e.TemplateCalcSnapshotJson,
            UpdatedAt = e.UpdatedAt,
        };

    [HttpGet]
    public async Task<ActionResult<SupervisorPolePrimeDraftResponseDto>> Get(
        [FromQuery] string supervisorUserId,
        [FromQuery] string poleId,
        [FromQuery] string period,
        [FromQuery] string templateId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(poleId) ||
            string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(templateId))
            return BadRequest(new { error = "supervisorUserId, poleId, period et templateId sont requis." });

        if (!store.SupervisorOwnsPole(supervisorUserId, poleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var entity = await db.SupervisorPolePrimeDrafts.AsNoTracking().FirstOrDefaultAsync(
            x => x.SupervisorUserId == supervisorUserId.Trim() && x.PoleId == poleId.Trim() &&
                 x.Period == period.Trim() && x.TemplateId == templateId.Trim(), ct);
        if (entity == null) return NotFound();
        return Ok(Map(entity));
    }

    [HttpPut]
    public async Task<ActionResult<SupervisorPolePrimeDraftResponseDto>> Upsert(
        [FromBody] UpsertSupervisorPolePrimeDraftRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.SupervisorUserId) || string.IsNullOrWhiteSpace(body.PoleId) ||
            string.IsNullOrWhiteSpace(body.Period) || string.IsNullOrWhiteSpace(body.TemplateId))
            return BadRequest(new { error = "Champs obligatoires manquants." });

        if (!store.SupervisorOwnsPole(body.SupervisorUserId, body.PoleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var poleSaisieToStore = PoleDraftPayloadNormalizer.NormalizePoleSaisieJson(body.SchemaJson, body.PoleSaisieJson);

        var now = DateTimeOffset.UtcNow;
        var entity = await db.SupervisorPolePrimeDrafts.FirstOrDefaultAsync(
            x => x.SupervisorUserId == body.SupervisorUserId.Trim() && x.PoleId == body.PoleId.Trim() &&
                 x.Period == body.Period.Trim() && x.TemplateId == body.TemplateId.Trim(), ct);

        if (entity == null)
        {
            entity = new SupervisorPolePrimeDraftEntity
            {
                Id = Guid.NewGuid(),
                SupervisorUserId = body.SupervisorUserId.Trim(),
                PoleId = body.PoleId.Trim(),
                Period = body.Period.Trim(),
                TemplateId = body.TemplateId.Trim(),
                TemplateDisplayName = body.TemplateDisplayName.Trim(),
                TemplateFormatVersion = body.TemplateFormatVersion,
                Status = string.IsNullOrWhiteSpace(body.Status) ? "Draft" : body.Status!.Trim(),
                SchemaJson = body.SchemaJson,
                PoleSaisieJson = poleSaisieToStore,
                ComputedJson = body.ComputedJson,
                TemplateCalcSnapshotJson = body.TemplateCalcSnapshotJson,
                UpdatedAt = now,
            };
            db.SupervisorPolePrimeDrafts.Add(entity);
        }
        else
        {
            entity.TemplateDisplayName = body.TemplateDisplayName.Trim();
            entity.TemplateFormatVersion = body.TemplateFormatVersion;
            if (!string.IsNullOrWhiteSpace(body.Status)) entity.Status = body.Status.Trim();
            entity.SchemaJson = body.SchemaJson;
            entity.PoleSaisieJson = poleSaisieToStore;
            entity.ComputedJson = body.ComputedJson;
            entity.TemplateCalcSnapshotJson = body.TemplateCalcSnapshotJson;
            entity.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }
}
