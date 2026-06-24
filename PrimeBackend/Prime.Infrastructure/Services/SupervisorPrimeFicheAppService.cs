using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class SupervisorPrimeFicheAppService(PrimeDbContext db) : ISupervisorPrimeFicheAppService
{
    private static SupervisorPrimeFicheResponseDto Map(SupervisorPrimeFicheEntity e) =>
        new()
        {
            Id = e.Id,
            SupervisorUserId = e.SupervisorUserId,
            CelluleId = e.CelluleId,
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

    public async Task<SupervisorPrimeFicheResponseDto> CreateAsync(
        CreateSupervisorPrimeFicheRequest body,
        CancellationToken ct = default)
    {
        var entity = new SupervisorPrimeFicheEntity
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = body.SupervisorUserId.Trim(),
            CelluleId = string.IsNullOrWhiteSpace(body.CelluleId) ? null : body.CelluleId.Trim(),
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
        return Map(entity);
    }

    public async Task<SupervisorPrimeFicheResponseDto> UpdateSaisieAsync(
        Guid id,
        UpdateSupervisorPrimeFicheSaisieRequest body,
        CancellationToken ct = default)
    {
        var entity = await db.SupervisorPrimeFiches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        if (entity.Status != "Draft")
            throw new PrimeApiException(409, "La fiche n'est plus modifiable.");

        entity.SaisieJson = body.SaisieJson;
        entity.ComputedJson = body.ComputedJson;
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<SupervisorPrimeFicheResponseDto> ValidateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.SupervisorPrimeFiches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        if (entity.Status == "Validated") return Map(entity);

        entity.Status = "Validated";
        entity.ValidatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<SupervisorPrimeFicheResponseDto>> ListAsync(
        string supervisorUserId,
        string? period,
        CancellationToken ct = default)
    {
        var q = db.SupervisorPrimeFiches.AsNoTracking().Where(x => x.SupervisorUserId == supervisorUserId);
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(x => x.Period == period);
        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.ConvertAll(Map);
    }
}
