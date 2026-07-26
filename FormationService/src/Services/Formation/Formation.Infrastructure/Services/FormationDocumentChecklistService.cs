using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Services;

public sealed class FormationDocumentChecklistService(
    FormationDbContext db,
    ILogger<FormationDocumentChecklistService> logger)
{
    private static readonly string[] DefaultTitles =
    [
        "Deux photos d'identité (labo)",
        "Copie carte nationale d'identité",
        "Attestation d'assurance",
        "RIB",
        "Certificat médical d'aptitude",
        "Copie diplôme / attestation",
    ];

    public async Task EnsureDefaultDefinitionsAsync(CancellationToken ct = default)
    {
        if (await db.FormationDocumentDefinitions.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        for (var i = 0; i < DefaultTitles.Length; i++)
        {
            db.FormationDocumentDefinitions.Add(new FormationDocumentDefinition
            {
                Title = DefaultTitles[i],
                SortOrder = i + 1,
                IsActive = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed checklist documents formation : {Count} définitions.", DefaultTitles.Length);
    }

    public async Task MaterializeForPathAsync(InitialTrainingPath path, CancellationToken ct = default)
    {
        var active = await db.FormationDocumentDefinitions.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);
        if (active.Count == 0) return;

        var existingDefIds = await db.FormationDocumentChecklistItems
            .Where(i => i.InitialTrainingPathId == path.Id)
            .Select(i => i.DefinitionId)
            .ToListAsync(ct);
        var existing = existingDefIds.ToHashSet();

        foreach (var def in active.Where(d => !existing.Contains(d.Id)))
        {
            db.FormationDocumentChecklistItems.Add(new FormationDocumentChecklistItem
            {
                EmployeeId = path.EmployeeId,
                InitialTrainingPathId = path.Id,
                DefinitionId = def.Id,
                IsReceived = false,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FormationDocumentDefinitionDto>> ListDefinitionsAsync(CancellationToken ct) =>
        await db.FormationDocumentDefinitions.AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Title)
            .Select(d => new FormationDocumentDefinitionDto(d.Id, d.Title, d.SortOrder, d.IsActive, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<FormationDocumentDefinitionDto> CreateDefinitionAsync(
        UpsertFormationDocumentDefinitionRequest request,
        CancellationToken ct)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Le titre du document est obligatoire.");

        var entity = new FormationDocumentDefinition
        {
            Title = title,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.FormationDocumentDefinitions.Add(entity);
        await db.SaveChangesAsync(ct);

        if (entity.IsActive)
            await MaterializeNewDefinitionForActivePathsAsync(entity, ct);

        return new FormationDocumentDefinitionDto(entity.Id, entity.Title, entity.SortOrder, entity.IsActive, entity.CreatedAt);
    }

    public async Task<FormationDocumentDefinitionDto?> UpdateDefinitionAsync(
        Guid id,
        UpsertFormationDocumentDefinitionRequest request,
        CancellationToken ct)
    {
        var entity = await db.FormationDocumentDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Le titre du document est obligatoire.");

        var wasActive = entity.IsActive;
        entity.Title = title;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        if (!wasActive && entity.IsActive)
            await MaterializeNewDefinitionForActivePathsAsync(entity, ct);

        return new FormationDocumentDefinitionDto(entity.Id, entity.Title, entity.SortOrder, entity.IsActive, entity.CreatedAt);
    }

    public async Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.FormationDocumentDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;

        var hasItems = await db.FormationDocumentChecklistItems.AnyAsync(i => i.DefinitionId == id, ct);
        if (hasItems)
        {
            entity.IsActive = false;
            await db.SaveChangesAsync(ct);
            return true;
        }

        db.FormationDocumentDefinitions.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<FormationDocumentChecklistItemDto>?> GetChecklistForPathAsync(
        Guid pathId,
        CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == pathId, ct);
        if (path is null) return null;

        await MaterializeForPathAsync(path, ct);
        return await LoadChecklistDtosAsync(pathId, ct);
    }

    public async Task<IReadOnlyList<FormationDocumentChecklistItemDto>?> GetChecklistForEmployeeAsync(
        Guid employeeId,
        CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths
            .Where(p => p.EmployeeId == employeeId
                        && p.Status != InitialTrainingStatus.Rejete
                        && p.Status != InitialTrainingStatus.EnProduction)
            .OrderByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (path is null) return Array.Empty<FormationDocumentChecklistItemDto>();

        await MaterializeForPathAsync(path, ct);
        return await LoadChecklistDtosAsync(path.Id, ct);
    }

    public async Task<FormationDocumentChecklistItemDto?> UpdateChecklistItemAsync(
        Guid pathId,
        Guid itemId,
        UpdateChecklistItemRequest request,
        CancellationToken ct)
    {
        var item = await db.FormationDocumentChecklistItems
            .Include(i => i.Definition)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.InitialTrainingPathId == pathId, ct);
        if (item is null) return null;

        item.IsReceived = request.IsReceived;
        item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.IsReceived)
        {
            item.ReceivedAt = DateTime.UtcNow;
            item.ReceivedBy = string.IsNullOrWhiteSpace(request.ReceivedBy) ? null : request.ReceivedBy.Trim();
        }
        else
        {
            item.ReceivedAt = null;
            item.ReceivedBy = null;
        }

        await db.SaveChangesAsync(ct);
        return ToItemDto(item);
    }

    public async Task<Dictionary<Guid, ChecklistSummary>> LoadSummariesAsync(
        IReadOnlyCollection<Guid> pathIds,
        CancellationToken ct)
    {
        if (pathIds.Count == 0)
            return new Dictionary<Guid, ChecklistSummary>();

        var rows = await db.FormationDocumentChecklistItems.AsNoTracking()
            .Where(i => i.InitialTrainingPathId != null && pathIds.Contains(i.InitialTrainingPathId.Value))
            .Select(i => new
            {
                PathId = i.InitialTrainingPathId!.Value,
                i.IsReceived,
                Title = i.Definition != null ? i.Definition.Title : "",
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.PathId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var list = g.ToList();
                    return new ChecklistSummary(
                        list.Count(x => x.IsReceived),
                        list.Count,
                        list.Where(x => !x.IsReceived && !string.IsNullOrWhiteSpace(x.Title))
                            .Select(x => x.Title)
                            .ToList());
                });
    }

    private async Task MaterializeNewDefinitionForActivePathsAsync(
        FormationDocumentDefinition def,
        CancellationToken ct)
    {
        var paths = await db.InitialTrainingPaths
            .Where(p => p.Status != InitialTrainingStatus.Rejete && p.Status != InitialTrainingStatus.EnProduction)
            .Select(p => new { p.Id, p.EmployeeId })
            .ToListAsync(ct);

        var existing = await db.FormationDocumentChecklistItems
            .Where(i => i.DefinitionId == def.Id && i.InitialTrainingPathId != null)
            .Select(i => i.InitialTrainingPathId!.Value)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        foreach (var path in paths.Where(p => !existingSet.Contains(p.Id)))
        {
            db.FormationDocumentChecklistItems.Add(new FormationDocumentChecklistItem
            {
                EmployeeId = path.EmployeeId,
                InitialTrainingPathId = path.Id,
                DefinitionId = def.Id,
                IsReceived = false,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<FormationDocumentChecklistItemDto>> LoadChecklistDtosAsync(
        Guid pathId,
        CancellationToken ct)
    {
        var items = await db.FormationDocumentChecklistItems.AsNoTracking()
            .Include(i => i.Definition)
            .Where(i => i.InitialTrainingPathId == pathId)
            .ToListAsync(ct);

        return items
            .OrderBy(i => i.Definition?.SortOrder ?? 0)
            .ThenBy(i => i.Definition?.Title ?? "")
            .Select(ToItemDto)
            .ToList();
    }

    private static FormationDocumentChecklistItemDto ToItemDto(FormationDocumentChecklistItem item) =>
        new(
            item.Id,
            item.DefinitionId,
            item.Definition?.Title ?? "",
            item.Definition?.SortOrder ?? 0,
            item.IsReceived,
            item.ReceivedAt,
            item.ReceivedBy,
            item.Note,
            item.InitialTrainingPathId);

    public sealed record ChecklistSummary(
        int ReceivedCount,
        int TotalCount,
        IReadOnlyList<string> MissingTitles);
}
