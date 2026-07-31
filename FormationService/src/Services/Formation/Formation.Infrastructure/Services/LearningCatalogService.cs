using System.Text.Json;
using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using MassTransit;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Services;

public sealed class LearningCatalogService(
    FormationDbContext db,
    IPublishEndpoint publish,
    ILogger<LearningCatalogService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    public async Task<IReadOnlyList<TrainingCatalogItemDto>> ListAsync(
        string? category,
        bool includeArchived,
        CancellationToken ct)
    {
        var q = db.TrainingCatalogItems.AsNoTracking().AsQueryable();
        if (!includeArchived)
            q = q.Where(x => x.Status != CatalogItemStatus.Archived && x.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(x => x.Category == category.Trim());

        var items = await q.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
        return await MapListAsync(items, includeTree: false, ct);
    }

    public async Task<TrainingCatalogItemDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.AsNoTracking()
            .Include(x => x.AudienceRules)
            .Include(x => x.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        var list = await MapListAsync([item], includeTree: true, ct);
        return list.FirstOrDefault();
    }

    public async Task<TrainingCatalogItemDto> CreateAsync(UpsertTrainingCatalogItemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre est obligatoire.");

        var item = new TrainingCatalogItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? "",
            Category = request.Category?.Trim() ?? "",
            DefaultGateMode = request.DefaultGateMode,
            AudienceMatchMode = request.AudienceMatchMode,
            CreatedByUserId = request.CreatedByUserId,
            Status = CatalogItemStatus.Draft,
            IsActive = true,
        };
        db.TrainingCatalogItems.Add(item);
        // Empty audience rule = public.
        db.TrainingCatalogAudienceRules.Add(new TrainingCatalogAudienceRule { CatalogItemId = item.Id });
        await db.SaveChangesAsync(ct);
        return (await GetAsync(item.Id, ct))!;
    }

    public async Task<TrainingCatalogItemDto> UpdateAsync(Guid id, UpsertTrainingCatalogItemRequest request, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre est obligatoire.");
        if (item.Status == CatalogItemStatus.Archived)
            throw new InvalidOperationException("Impossible de modifier une formation archivée.");

        item.Title = request.Title.Trim();
        item.Description = request.Description?.Trim() ?? "";
        item.Category = request.Category?.Trim() ?? "";
        item.DefaultGateMode = request.DefaultGateMode;
        item.AudienceMatchMode = request.AudienceMatchMode;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(item.Id, ct))!;
    }

    public async Task<TrainingCatalogItemDto> PublishAsync(Guid id, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");
        item.Status = CatalogItemStatus.Published;
        item.IsActive = true;
        item.PublishedAt = DateTime.UtcNow;
        item.ArchivedAt = null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(item.Id, ct))!;
    }

    public async Task<TrainingCatalogItemDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");
        item.Status = CatalogItemStatus.Archived;
        item.IsActive = false;
        item.ArchivedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(item.Id, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var linked = await db.TrainingSessions.AnyAsync(s => s.CatalogItemId == id, ct);
        if (linked)
            throw new InvalidOperationException("Des sessions sont liées — archivez la formation au lieu de la supprimer.");

        var item = await db.TrainingCatalogItems
            .Include(x => x.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");

        foreach (var res in item.Modules.SelectMany(m => m.Lessons).SelectMany(l => l.Resources))
            TryDeleteFile(res.StoragePath);

        db.TrainingCatalogItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TrainingCatalogAudienceDto> UpsertAudienceAsync(
        Guid catalogItemId,
        UpsertTrainingCatalogAudienceRequest request,
        CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems
            .Include(x => x.AudienceRules)
            .FirstOrDefaultAsync(x => x.Id == catalogItemId, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");

        item.AudienceMatchMode = request.MatchMode;
        item.UpdatedAt = DateTime.UtcNow;

        var rule = item.AudienceRules.FirstOrDefault();
        if (rule is null)
        {
            rule = new TrainingCatalogAudienceRule { CatalogItemId = catalogItemId };
            db.TrainingCatalogAudienceRules.Add(rule);
        }

        rule.RolesJson = JsonSerializer.Serialize(NormalizeStrings(request.Roles), JsonOpts);
        rule.StructureKeysJson = JsonSerializer.Serialize(NormalizeStrings(request.StructureKeys), JsonOpts);
        rule.UserIdsJson = JsonSerializer.Serialize(
            request.UserIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [],
            JsonOpts);

        await db.SaveChangesAsync(ct);
        var beneficiaries = await ResolveAudienceAsync(item.Id, ct);
        return new TrainingCatalogAudienceDto(
            item.AudienceMatchMode,
            ParseStringList(rule.RolesJson),
            ParseStringList(rule.StructureKeysJson),
            ParseGuidList(rule.UserIdsJson),
            beneficiaries.Count);
    }

    public async Task<TrainingModuleDto> UpsertModuleAsync(
        Guid catalogItemId,
        Guid? moduleId,
        UpsertTrainingModuleRequest request,
        CancellationToken ct)
    {
        _ = await db.TrainingCatalogItems.FirstOrDefaultAsync(x => x.Id == catalogItemId, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre du module est obligatoire.");

        TrainingModule module;
        if (moduleId is Guid mid)
        {
            module = await db.TrainingModules.FirstOrDefaultAsync(m => m.Id == mid && m.CatalogItemId == catalogItemId, ct)
                ?? throw new InvalidOperationException("Module introuvable.");
            module.Title = request.Title.Trim();
            module.Description = request.Description?.Trim() ?? "";
            module.SortOrder = request.SortOrder;
        }
        else
        {
            module = new TrainingModule
            {
                CatalogItemId = catalogItemId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? "",
                SortOrder = request.SortOrder,
            };
            db.TrainingModules.Add(module);
        }

        await TouchCatalogAsync(catalogItemId, ct);
        await db.SaveChangesAsync(ct);
        return await MapModuleAsync(module.Id, ct);
    }

    public async Task DeleteModuleAsync(Guid catalogItemId, Guid moduleId, CancellationToken ct)
    {
        var module = await db.TrainingModules
            .Include(m => m.Lessons).ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.CatalogItemId == catalogItemId, ct)
            ?? throw new InvalidOperationException("Module introuvable.");
        foreach (var res in module.Lessons.SelectMany(l => l.Resources))
            TryDeleteFile(res.StoragePath);
        db.TrainingModules.Remove(module);
        await TouchCatalogAsync(catalogItemId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TrainingLessonDto> UpsertLessonAsync(
        Guid moduleId,
        Guid? lessonId,
        UpsertTrainingLessonRequest request,
        CancellationToken ct)
    {
        var module = await db.TrainingModules.FirstOrDefaultAsync(m => m.Id == moduleId, ct)
            ?? throw new InvalidOperationException("Module introuvable.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre de la leçon est obligatoire.");

        TrainingLesson lesson;
        if (lessonId is Guid lid)
        {
            lesson = await db.TrainingLessons.FirstOrDefaultAsync(l => l.Id == lid && l.ModuleId == moduleId, ct)
                ?? throw new InvalidOperationException("Leçon introuvable.");
            lesson.Title = request.Title.Trim();
            lesson.Description = request.Description?.Trim() ?? "";
            lesson.SortOrder = request.SortOrder;
            lesson.IsRequired = request.IsRequired;
        }
        else
        {
            lesson = new TrainingLesson
            {
                ModuleId = moduleId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? "",
                SortOrder = request.SortOrder,
                IsRequired = request.IsRequired,
            };
            db.TrainingLessons.Add(lesson);
        }

        await TouchCatalogAsync(module.CatalogItemId, ct);
        await db.SaveChangesAsync(ct);
        return await MapLessonAsync(lesson.Id, ct);
    }

    public async Task DeleteLessonAsync(Guid moduleId, Guid lessonId, CancellationToken ct)
    {
        var module = await db.TrainingModules.FirstOrDefaultAsync(m => m.Id == moduleId, ct)
            ?? throw new InvalidOperationException("Module introuvable.");
        var lesson = await db.TrainingLessons
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == lessonId && l.ModuleId == moduleId, ct)
            ?? throw new InvalidOperationException("Leçon introuvable.");
        foreach (var res in lesson.Resources)
            TryDeleteFile(res.StoragePath);
        db.TrainingLessons.Remove(lesson);
        await TouchCatalogAsync(module.CatalogItemId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TrainingResourceDto> UpsertResourceAsync(
        Guid lessonId,
        Guid? resourceId,
        UpsertTrainingResourceRequest request,
        CancellationToken ct)
    {
        var lesson = await db.TrainingLessons.Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new InvalidOperationException("Leçon introuvable.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre de la ressource est obligatoire.");

        TrainingResource resource;
        if (resourceId is Guid rid)
        {
            resource = await db.TrainingResources.FirstOrDefaultAsync(r => r.Id == rid && r.LessonId == lessonId, ct)
                ?? throw new InvalidOperationException("Ressource introuvable.");
            resource.Type = request.Type;
            resource.Title = request.Title.Trim();
            resource.Url = request.Url?.Trim();
            resource.TextContent = request.TextContent;
            resource.SortOrder = request.SortOrder;
            resource.DurationMinutes = request.DurationMinutes;
        }
        else
        {
            resource = new TrainingResource
            {
                LessonId = lessonId,
                Type = request.Type,
                Title = request.Title.Trim(),
                Url = request.Url?.Trim(),
                TextContent = request.TextContent,
                SortOrder = request.SortOrder,
                DurationMinutes = request.DurationMinutes,
            };
            db.TrainingResources.Add(resource);
        }

        await TouchCatalogAsync(lesson.Module!.CatalogItemId, ct);
        await db.SaveChangesAsync(ct);
        return MapResource(resource);
    }

    public async Task<TrainingResourceDto> UploadResourceFileAsync(
        Guid lessonId,
        string fileName,
        string contentType,
        Stream content,
        string? rootPath,
        TrainingResourceType type,
        string? title,
        CancellationToken ct)
    {
        var lesson = await db.TrainingLessons.Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new InvalidOperationException("Leçon introuvable.");

        var root = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "learning")
            : rootPath;
        Directory.CreateDirectory(root);

        var safeName = $"{lessonId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(root, safeName);
        await using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        var resource = new TrainingResource
        {
            LessonId = lessonId,
            Type = type,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim(),
            StoragePath = fullPath,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Url = $"/api/formations/catalog/resources/file/{Guid.Empty}", // patched after save
        };
        db.TrainingResources.Add(resource);
        await TouchCatalogAsync(lesson.Module!.CatalogItemId, ct);
        await db.SaveChangesAsync(ct);

        resource.Url = $"/api/formations/catalog/resources/file/{resource.Id}";
        await db.SaveChangesAsync(ct);
        return MapResource(resource);
    }

    public async Task<(TrainingResource Resource, byte[] Bytes)?> GetResourceFileAsync(Guid resourceId, CancellationToken ct)
    {
        var resource = await db.TrainingResources.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct);
        if (resource is null || string.IsNullOrWhiteSpace(resource.StoragePath) || !File.Exists(resource.StoragePath))
            return null;
        var bytes = await File.ReadAllBytesAsync(resource.StoragePath, ct);
        return (resource, bytes);
    }

    public async Task DeleteResourceAsync(Guid lessonId, Guid resourceId, CancellationToken ct)
    {
        var lesson = await db.TrainingLessons.Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new InvalidOperationException("Leçon introuvable.");
        var resource = await db.TrainingResources.FirstOrDefaultAsync(r => r.Id == resourceId && r.LessonId == lessonId, ct)
            ?? throw new InvalidOperationException("Ressource introuvable.");
        TryDeleteFile(resource.StoragePath);
        db.TrainingResources.Remove(resource);
        await TouchCatalogAsync(lesson.Module!.CatalogItemId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TrainingSessionDto> LinkSessionCatalogAsync(
        Guid sessionId,
        LinkSessionCatalogRequest request,
        CancellationToken ct)
    {
        var session = await db.TrainingSessions.Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        if (request.CatalogItemId is Guid catalogId)
        {
            var catalog = await db.TrainingCatalogItems.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == catalogId, ct)
                ?? throw new InvalidOperationException("Formation catalogue introuvable.");
            if (catalog.Status == CatalogItemStatus.Archived)
                throw new InvalidOperationException("Impossible de lier une formation archivée.");

            session.CatalogItemId = catalogId;
            session.LearningGateMode = request.LearningGateMode ?? catalog.DefaultGateMode;
            session.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            if (request.AssignAudience)
                await AssignAudienceToSessionAsync(sessionId, catalogId, ct);
        }
        else
        {
            session.CatalogItemId = null;
            session.LearningGateMode = request.LearningGateMode;
            session.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var mapped = await db.TrainingSessions.AsNoTracking()
            .FirstAsync(s => s.Id == sessionId, ct);
        var count = await db.TrainingAssignments.CountAsync(a => a.SessionId == sessionId, ct);
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct);
        var hasReport = await db.TrainingSessionReports.AnyAsync(r => r.SessionId == sessionId, ct);
        return new TrainingSessionDto(
            mapped.Id,
            mapped.Title,
            mapped.Description,
            mapped.Type,
            mapped.AnimatorKind,
            mapped.AnimatorUserId,
            mapped.ExternalAnimatorName,
            mapped.ExternalAnimatorOrganization,
            mapped.ExternalAnimatorEmail,
            mapped.ExternalAnimatorPhone,
            mapped.PlannedStart,
            mapped.PlannedEnd,
            mapped.Capacity,
            mapped.Status,
            count,
            mapped.ProgramId,
            mapped.SequenceNumber,
            hasReport,
            quiz?.Id,
            quiz?.Status.ToString(),
            mapped.CatalogItemId,
            mapped.LearningGateMode?.ToString());
    }

    public async Task<int> AssignAudienceToSessionAsync(Guid sessionId, Guid catalogItemId, CancellationToken ct)
    {
        var session = await db.TrainingSessions.Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        var beneficiaries = await ResolveAudienceAsync(catalogItemId, ct);
        if (beneficiaries.Count == 0)
        {
            logger.LogInformation("Audience vide pour catalogue {CatalogId} — aucune affectation.", catalogItemId);
            return 0;
        }

        var remaining = Math.Max(0, session.Capacity - session.Assignments.Count);
        var toAssign = beneficiaries
            .Where(b => session.Assignments.All(a => a.EmployeeId != b.EmployeId))
            .Take(remaining)
            .Select(b => new AssignTrainingEmployeeItem
            {
                EmployeeId = b.EmployeId,
                EmployeeName = $"{b.Prenom} {b.Nom}".Trim(),
            })
            .ToList();

        if (toAssign.Count == 0) return 0;

        var newly = new List<(Guid EmployeeId, string EmployeeName)>();
        foreach (var item in toAssign)
        {
            db.TrainingAssignments.Add(new TrainingAssignment
            {
                SessionId = sessionId,
                EmployeeId = item.EmployeeId,
                EmployeeName = item.EmployeeName,
            });
            newly.Add((item.EmployeeId, item.EmployeeName));
        }

        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        foreach (var (employeeId, employeeName) in newly)
        {
            await publish.Publish(new TrainingSessionAssignedMessage
            {
                SessionId = sessionId,
                Title = session.Title,
                PlannedStart = session.PlannedStart,
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                AssignedAt = DateTime.UtcNow,
            }, ct);
        }

        return newly.Count;
    }

    public async Task EnsureCanAccessCatalogAsync(Guid catalogItemId, Guid employeeId, CancellationToken ct)
    {
        var beneficiaries = await ResolveAudienceAsync(catalogItemId, ct);
        // Empty audience = public.
        var rule = await db.TrainingCatalogAudienceRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.CatalogItemId == catalogItemId, ct);
        var roles = ParseStringList(rule?.RolesJson);
        var structures = ParseStringList(rule?.StructureKeysJson);
        var users = ParseGuidList(rule?.UserIdsJson);
        if (roles.Count == 0 && structures.Count == 0 && users.Count == 0)
            return;

        if (beneficiaries.All(b => b.EmployeId != employeeId))
            throw new InvalidOperationException("Vous n'êtes pas dans l'audience de cette formation.");
    }

    public async Task<CatalogPlayerDto> GetPlayerAsync(Guid sessionId, Guid employeeId, CancellationToken ct)
    {
        var session = await db.TrainingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");
        if (session.CatalogItemId is null)
            throw new InvalidOperationException("Cette session n'a pas de contenu e-learning lié.");

        var assignment = await db.TrainingAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.EmployeeId == employeeId, ct)
            ?? throw new InvalidOperationException("Vous n'êtes pas affecté à cette séance.");

        await EnsureCanAccessCatalogAsync(session.CatalogItemId.Value, employeeId, ct);

        var catalog = await db.TrainingCatalogItems.AsNoTracking()
            .Include(c => c.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(c => c.Id == session.CatalogItemId, ct)
            ?? throw new InvalidOperationException("Formation catalogue introuvable.");

        var progress = await db.TrainingLessonProgresses.AsNoTracking()
            .Where(p => p.AssignmentId == assignment.Id)
            .ToDictionaryAsync(p => p.LessonId, ct);

        var modules = catalog.Modules.OrderBy(m => m.SortOrder).Select(m => new TrainingModuleDto(
            m.Id,
            m.CatalogItemId,
            m.Title,
            m.Description,
            m.SortOrder,
            m.Lessons.OrderBy(l => l.SortOrder).Select(l =>
            {
                progress.TryGetValue(l.Id, out var p);
                return new TrainingLessonDto(
                    l.Id,
                    l.ModuleId,
                    l.Title,
                    l.Description,
                    l.SortOrder,
                    l.IsRequired,
                    l.Resources.OrderBy(r => r.SortOrder).Select(MapResource).ToList(),
                    p?.CompletedAt is not null,
                    p?.ProgressPercent ?? 0m);
            }).ToList())).ToList();

        var required = modules.SelectMany(m => m.Lessons).Where(l => l.IsRequired).ToList();
        var done = required.Count(l => l.IsCompleted);
        var percent = required.Count == 0 ? 100m : Math.Round((decimal)done / required.Count * 100m, 1);

        var gate = session.LearningGateMode ?? catalog.DefaultGateMode;
        var (canTake, reason) = await EvaluateQuizGateAsync(session, assignment, catalog.Id, ct);

        return new CatalogPlayerDto(
            catalog.Id,
            session.Id,
            assignment.Id,
            catalog.Title,
            catalog.Description,
            catalog.Category,
            gate,
            percent,
            required.Count,
            done,
            canTake,
            reason,
            modules);
    }

    public async Task<TrainingLessonDto> CompleteLessonAsync(
        Guid sessionId,
        Guid lessonId,
        CompleteLessonRequest request,
        CancellationToken ct)
    {
        var session = await db.TrainingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");
        if (session.CatalogItemId is null)
            throw new InvalidOperationException("Session sans catalogue.");

        var assignment = await db.TrainingAssignments
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.EmployeeId == request.EmployeeId, ct)
            ?? throw new InvalidOperationException("Affectation introuvable.");

        var lesson = await db.TrainingLessons.AsNoTracking()
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new InvalidOperationException("Leçon introuvable.");
        if (lesson.Module!.CatalogItemId != session.CatalogItemId)
            throw new InvalidOperationException("Leçon hors catalogue de la session.");

        var progress = await db.TrainingLessonProgresses
            .FirstOrDefaultAsync(p => p.AssignmentId == assignment.Id && p.LessonId == lessonId, ct);
        if (progress is null)
        {
            progress = new TrainingLessonProgress
            {
                AssignmentId = assignment.Id,
                LessonId = lessonId,
                StartedAt = DateTime.UtcNow,
            };
            db.TrainingLessonProgresses.Add(progress);
        }

        progress.LastResourceId = request.LastResourceId;
        progress.ProgressPercent = 100m;
        progress.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapLessonAsync(lessonId, ct, assignment.Id);
    }

    public async Task<(bool CanTake, string? Reason)> EvaluateQuizGateAsync(
        TrainingSession session,
        TrainingAssignment assignment,
        Guid? catalogItemId,
        CancellationToken ct)
    {
        var gate = session.LearningGateMode
            ?? (catalogItemId is null ? LearningGateMode.Attendance : LearningGateMode.Content);

        var present = assignment.Status == TrainingAssignmentStatus.Completed;
        var contentOk = true;
        if (gate is LearningGateMode.Content or LearningGateMode.Both)
        {
            if (catalogItemId is null)
                contentOk = true;
            else
            {
                var requiredLessonIds = await (
                    from l in db.TrainingLessons.AsNoTracking()
                    join m in db.TrainingModules.AsNoTracking() on l.ModuleId equals m.Id
                    where m.CatalogItemId == catalogItemId && l.IsRequired
                    select l.Id).ToListAsync(ct);

                if (requiredLessonIds.Count > 0)
                {
                    var done = await db.TrainingLessonProgresses.AsNoTracking()
                        .CountAsync(p => p.AssignmentId == assignment.Id
                            && requiredLessonIds.Contains(p.LessonId)
                            && p.CompletedAt != null, ct);
                    contentOk = done >= requiredLessonIds.Count;
                }
            }
        }

        return gate switch
        {
            LearningGateMode.Attendance when !present =>
                (false, "Présence requise avant de passer le quiz."),
            LearningGateMode.Content when !contentOk =>
                (false, "Terminez les leçons obligatoires avant de passer le quiz."),
            LearningGateMode.Both when !present =>
                (false, "Présence requise avant de passer le quiz."),
            LearningGateMode.Both when !contentOk =>
                (false, "Terminez les leçons obligatoires avant de passer le quiz."),
            _ => (true, null),
        };
    }

    public async Task<LearningQuizStatsDto> GetLearningStatsAsync(CancellationToken ct)
    {
        var catalogs = await db.TrainingCatalogItems.AsNoTracking()
            .CountAsync(c => c.Status != CatalogItemStatus.Archived, ct);
        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.CatalogItemId != null)
            .Select(s => new { s.Id, s.Title, s.CatalogItemId })
            .ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var quizzes = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .Where(q => sessionIds.Contains(q.SessionId))
            .ToListAsync(ct);
        var quizIds = quizzes.Select(q => q.Id).ToList();
        var attempts = await db.TrainingQuizAttempts.AsNoTracking()
            .Where(a => quizIds.Contains(a.QuizId) && a.FinalScore != null)
            .ToListAsync(ct);

        var categories = await db.TrainingCatalogItems.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Category, ct);

        double avg = attempts.Count == 0 ? 0 : Math.Round(attempts.Average(a => (double)(a.FinalScore ?? 0)), 1);
        double best = attempts.Count == 0 ? 0 : Math.Round(attempts.Max(a => (double)(a.FinalScore ?? 0)), 1);
        var graded = attempts.Where(a => a.IsGraded && a.Passed != null).ToList();
        double passRate = graded.Count == 0
            ? 0
            : Math.Round(graded.Count(a => a.Passed == true) * 100.0 / graded.Count, 1);

        var bySession = sessions.Select(s =>
        {
            var quiz = quizzes.FirstOrDefault(q => q.SessionId == s.Id);
            var atts = quiz is null ? [] : attempts.Where(a => a.QuizId == quiz.Id).ToList();
            var g = atts.Where(a => a.IsGraded && a.Passed != null).ToList();
            string? cat = null;
            if (s.CatalogItemId is Guid cid)
                categories.TryGetValue(cid, out cat);
            return new LearningQuizStatsBySessionDto(
                s.Id,
                s.Title,
                cat,
                quiz?.Questions.Count ?? 0,
                atts.Count,
                atts.Count == 0 ? 0 : Math.Round(atts.Average(a => (double)(a.FinalScore ?? 0)), 1),
                atts.Count == 0 ? 0 : Math.Round(atts.Max(a => (double)(a.FinalScore ?? 0)), 1),
                g.Count == 0 ? 0 : Math.Round(g.Count(a => a.Passed == true) * 100.0 / g.Count, 1));
        }).ToList();

        return new LearningQuizStatsDto(
            catalogs,
            sessions.Count,
            quizzes.Sum(q => q.Questions.Count),
            attempts.Count,
            avg,
            best,
            passRate,
            bySession);
    }

    public async Task<IReadOnlyList<LearningQuizResultExportRowDto>> ExportResultsAsync(
        Guid? sessionId,
        CancellationToken ct)
    {
        var q = db.TrainingQuizAttempts.AsNoTracking().AsQueryable();
        if (sessionId is Guid sid)
        {
            var quiz = await db.TrainingQuizzes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SessionId == sid, ct);
            if (quiz is null) return [];
            q = q.Where(a => a.QuizId == quiz.Id);
        }

        var attempts = await q.OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);
        var assignmentIds = attempts.Select(a => a.AssignmentId).Distinct().ToList();
        var assignments = await db.TrainingAssignments.AsNoTracking()
            .Where(a => assignmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var sessionIds = assignments.Values.Select(a => a.SessionId).Distinct().ToList();
        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);
        var employeeIds = attempts.Select(a => a.EmployeeId).Distinct().ToList();
        var employees = await db.EmployeAnnuaires.AsNoTracking()
            .Where(e => employeeIds.Contains(e.EmployeId))
            .ToDictionaryAsync(e => e.EmployeId, ct);

        return attempts.Select(a =>
        {
            assignments.TryGetValue(a.AssignmentId, out var asg);
            sessions.TryGetValue(asg?.SessionId ?? Guid.Empty, out var sess);
            employees.TryGetValue(a.EmployeeId, out var emp);
            return new LearningQuizResultExportRowDto(
                asg?.EmployeeName ?? (emp is null ? "" : $"{emp.Prenom} {emp.Nom}".Trim()),
                emp?.Email ?? "",
                emp?.Role ?? "",
                emp?.StructureKey ?? "",
                sess?.Title ?? "",
                a.FinalScore,
                a.Passed,
                a.AttemptNumber,
                a.SubmittedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<EmployeAnnuaire>> ResolveAudienceAsync(Guid catalogItemId, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.AsNoTracking()
            .Include(c => c.AudienceRules)
            .FirstOrDefaultAsync(c => c.Id == catalogItemId, ct);
        if (item is null) return [];

        var rule = item.AudienceRules.FirstOrDefault();
        var roles = ParseStringList(rule?.RolesJson);
        var structures = ParseStringList(rule?.StructureKeysJson);
        var users = ParseGuidList(rule?.UserIdsJson);

        // Public: no filters.
        if (roles.Count == 0 && structures.Count == 0 && users.Count == 0)
            return await db.EmployeAnnuaires.AsNoTracking().ToListAsync(ct);

        var all = await db.EmployeAnnuaires.AsNoTracking().ToListAsync(ct);
        return all.Where(e => MatchesAudience(e, roles, structures, users, item.AudienceMatchMode)).ToList();
    }

    private static bool MatchesAudience(
        EmployeAnnuaire e,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> structures,
        IReadOnlyList<Guid> users,
        CatalogAudienceMatchMode mode)
    {
        var roleOk = roles.Count == 0
            || roles.Any(r => e.Role.Equals(r, StringComparison.OrdinalIgnoreCase));
        var structureOk = structures.Count == 0
            || (!string.IsNullOrWhiteSpace(e.StructureKey)
                && structures.Any(s => e.StructureKey!.Equals(s, StringComparison.OrdinalIgnoreCase)));
        var userOk = users.Count == 0 || users.Contains(e.EmployeId);

        // Dimensions with empty filter are ignored.
        var checks = new List<bool>();
        if (roles.Count > 0) checks.Add(roleOk);
        if (structures.Count > 0) checks.Add(structureOk);
        if (users.Count > 0) checks.Add(userOk);
        if (checks.Count == 0) return true;

        return mode == CatalogAudienceMatchMode.MatchAll
            ? checks.All(x => x)
            : checks.Any(x => x);
    }

    private async Task<IReadOnlyList<TrainingCatalogItemDto>> MapListAsync(
        IReadOnlyList<TrainingCatalogItem> items,
        bool includeTree,
        CancellationToken ct)
    {
        var ids = items.Select(i => i.Id).ToList();
        var modules = await db.TrainingModules.AsNoTracking()
            .Where(m => ids.Contains(m.CatalogItemId))
            .ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var lessons = await db.TrainingLessons.AsNoTracking()
            .Where(l => moduleIds.Contains(l.ModuleId))
            .ToListAsync(ct);
        var lessonIds = lessons.Select(l => l.Id).ToList();
        var resources = await db.TrainingResources.AsNoTracking()
            .Where(r => lessonIds.Contains(r.LessonId))
            .ToListAsync(ct);
        var rules = await db.TrainingCatalogAudienceRules.AsNoTracking()
            .Where(r => ids.Contains(r.CatalogItemId))
            .ToListAsync(ct);

        var result = new List<TrainingCatalogItemDto>();
        foreach (var item in items)
        {
            var itemModules = modules.Where(m => m.CatalogItemId == item.Id).ToList();
            var itemLessons = lessons.Where(l => itemModules.Any(m => m.Id == l.ModuleId)).ToList();
            var itemResources = resources.Where(r => itemLessons.Any(l => l.Id == r.LessonId)).ToList();
            var rule = rules.FirstOrDefault(r => r.CatalogItemId == item.Id);
            var audience = new TrainingCatalogAudienceDto(
                item.AudienceMatchMode,
                ParseStringList(rule?.RolesJson),
                ParseStringList(rule?.StructureKeysJson),
                ParseGuidList(rule?.UserIdsJson),
                0);

            IReadOnlyList<TrainingModuleDto>? tree = null;
            if (includeTree)
            {
                tree = itemModules.OrderBy(m => m.SortOrder).Select(m => new TrainingModuleDto(
                    m.Id,
                    m.CatalogItemId,
                    m.Title,
                    m.Description,
                    m.SortOrder,
                    itemLessons.Where(l => l.ModuleId == m.Id).OrderBy(l => l.SortOrder).Select(l =>
                        new TrainingLessonDto(
                            l.Id,
                            l.ModuleId,
                            l.Title,
                            l.Description,
                            l.SortOrder,
                            l.IsRequired,
                            itemResources.Where(r => r.LessonId == l.Id).OrderBy(r => r.SortOrder)
                                .Select(MapResource).ToList())).ToList())).ToList();
            }

            result.Add(new TrainingCatalogItemDto(
                item.Id,
                item.Title,
                item.Description,
                item.Category,
                item.Status,
                item.IsActive,
                item.DefaultGateMode,
                item.AudienceMatchMode,
                item.CreatedAt,
                item.UpdatedAt,
                item.PublishedAt,
                item.ArchivedAt,
                itemModules.Count,
                itemLessons.Count,
                itemResources.Count,
                audience,
                tree));
        }

        return result;
    }

    private async Task<TrainingModuleDto> MapModuleAsync(Guid moduleId, CancellationToken ct)
    {
        var m = await db.TrainingModules.AsNoTracking()
            .Include(x => x.Lessons).ThenInclude(l => l.Resources)
            .FirstAsync(x => x.Id == moduleId, ct);
        return new TrainingModuleDto(
            m.Id,
            m.CatalogItemId,
            m.Title,
            m.Description,
            m.SortOrder,
            m.Lessons.OrderBy(l => l.SortOrder).Select(l => new TrainingLessonDto(
                l.Id,
                l.ModuleId,
                l.Title,
                l.Description,
                l.SortOrder,
                l.IsRequired,
                l.Resources.OrderBy(r => r.SortOrder).Select(MapResource).ToList())).ToList());
    }

    private async Task<TrainingLessonDto> MapLessonAsync(Guid lessonId, CancellationToken ct, Guid? assignmentId = null)
    {
        var l = await db.TrainingLessons.AsNoTracking()
            .Include(x => x.Resources)
            .FirstAsync(x => x.Id == lessonId, ct);
        bool completed = false;
        decimal percent = 0m;
        if (assignmentId is Guid aid)
        {
            var p = await db.TrainingLessonProgresses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AssignmentId == aid && x.LessonId == lessonId, ct);
            completed = p?.CompletedAt is not null;
            percent = p?.ProgressPercent ?? 0m;
        }

        return new TrainingLessonDto(
            l.Id,
            l.ModuleId,
            l.Title,
            l.Description,
            l.SortOrder,
            l.IsRequired,
            l.Resources.OrderBy(r => r.SortOrder).Select(MapResource).ToList(),
            completed,
            percent);
    }

    private static TrainingResourceDto MapResource(TrainingResource r) =>
        new(
            r.Id,
            r.LessonId,
            r.Type,
            r.Title,
            r.Url,
            r.ContentType,
            r.FileName,
            r.TextContent,
            r.SortOrder,
            r.DurationMinutes,
            string.IsNullOrWhiteSpace(r.StoragePath) ? null : $"/api/formations/catalog/resources/file/{r.Id}");

    private async Task TouchCatalogAsync(Guid catalogItemId, CancellationToken ct)
    {
        var item = await db.TrainingCatalogItems.FirstOrDefaultAsync(c => c.Id == catalogItemId, ct);
        if (item is null) return;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { File.Delete(path); } catch { /* ignore */ }
    }

    private static List<string> NormalizeStrings(IEnumerable<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static List<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch { return []; }
    }
}
