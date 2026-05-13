namespace PrimeBackend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

[ApiController]
[Route("api/prime/supervisor-fiches")]
public sealed class SupervisorPrimeFicheController : ControllerBase
{
    private readonly IServiceProvider _services;

    public SupervisorPrimeFicheController(IServiceProvider services) => _services = services;

    private PrimeDbContext? Db => _services.GetService(typeof(PrimeDbContext)) as PrimeDbContext;

    private static SupervisorPrimeFicheResponseDto Map(SupervisorPrimeFicheEntity e) =>
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
            SaisieJson = e.SaisieJson,
            ComputedJson = e.ComputedJson,
            CreatedAt = e.CreatedAt,
            ValidatedAt = e.ValidatedAt,
        };

    [HttpPost]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> Create([FromBody] CreateSupervisorPrimeFicheRequest body, CancellationToken ct)
    {
        var db = Db;
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée (connection string)." });

        var entity = new SupervisorPrimeFicheEntity
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = body.SupervisorUserId.Trim(),
            PoleId = string.IsNullOrWhiteSpace(body.PoleId) ? null : body.PoleId.Trim(),
            Period = body.Period.Trim(),
            TemplateId = body.TemplateId.Trim(),
            TemplateDisplayName = body.TemplateDisplayName.Trim(),
            TemplateFormatVersion = body.TemplateFormatVersion,
            Status = "Draft",
            SchemaJson = body.SchemaJson,
            SaisieJson = body.SaisieJson,
            ComputedJson = body.ComputedJson,
            CreatedAt = DateTimeOffset.UtcNow,
            ValidatedAt = null,
        };

        db.SupervisorPrimeFiches.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpPut("{id:guid}/saisie")]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> UpdateSaisie(Guid id, [FromBody] UpdateSupervisorPrimeFicheSaisieRequest body, CancellationToken ct)
    {
        var db = Db;
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var entity = await db.SupervisorPrimeFiches.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();
        if (entity.Status != "Draft") return Conflict(new { error = "La fiche n'est plus modifiable." });

        entity.SaisieJson = body.SaisieJson;
        entity.ComputedJson = body.ComputedJson;
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> Validate(Guid id, CancellationToken ct)
    {
        var db = Db;
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var entity = await db.SupervisorPrimeFiches.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();
        if (entity.Status == "Validated") return Ok(Map(entity));

        entity.Status = "Validated";
        entity.ValidatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpGet]
    public async Task<ActionResult<List<SupervisorPrimeFicheResponseDto>>> List([FromQuery] string supervisorUserId, [FromQuery] string? period, CancellationToken ct)
    {
        var db = Db;
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var q = db.SupervisorPrimeFiches.AsNoTracking().Where(x => x.SupervisorUserId == supervisorUserId);
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(x => x.Period == period);
        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Ok(list.ConvertAll(Map));
    }
}
