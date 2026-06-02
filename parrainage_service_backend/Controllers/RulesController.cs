using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/rules")]
public sealed class RulesController(ParrainageDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReferralRuleDto>>> List(CancellationToken ct)
    {
        var rows = await db.ReferralRules.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return Ok(rows.Select(r => r.ToDto()).ToList());
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ReferralRuleDto>> Upsert(string id, [FromBody] UpsertRuleRequest body, CancellationToken ct)
    {
        var entity = await db.ReferralRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null)
        {
            entity = new ReferralRuleEntity { Id = id, CreatedAt = DateTimeOffset.UtcNow };
            db.ReferralRules.Add(entity);
        }

        if (body.Name != null) entity.Name = body.Name;
        if (body.Type != null) entity.Type = body.Type;
        if (body.Value.HasValue) entity.Value = body.Value.Value;
        if (body.Target != null) entity.Target = body.Target;
        if (body.Status != null) entity.Status = body.Status;

        await db.SaveChangesAsync(ct);
        return Ok(entity.ToDto());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var entity = await db.ReferralRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { error = $"Règle introuvable : {id}" });
        db.ReferralRules.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
