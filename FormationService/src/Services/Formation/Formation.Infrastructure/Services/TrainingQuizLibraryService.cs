using System.Text.Json;
using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Formation.Infrastructure.Services;

public sealed class TrainingQuizLibraryService(
    FormationDbContext db,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    public async Task<IReadOnlyList<TrainingQuizTemplateListItemDto>> ListTemplatesAsync(
        bool includeArchived,
        CancellationToken ct)
    {
        var q = db.TrainingQuizTemplates.AsNoTracking().AsQueryable();
        if (!includeArchived)
            q = q.Where(t => t.Status != CatalogItemStatus.Archived);

        var templates = await q.OrderByDescending(t => t.UpdatedAt).ToListAsync(ct);
        if (templates.Count == 0) return [];

        var ids = templates.Select(t => t.Id).ToList();
        var questionCounts = await db.TrainingQuizTemplateQuestions.AsNoTracking()
            .Where(x => ids.Contains(x.TemplateId))
            .GroupBy(x => x.TemplateId)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, ct);

        var usageCounts = await db.TrainingQuizzes.AsNoTracking()
            .Where(x => x.TemplateId != null && ids.Contains(x.TemplateId.Value))
            .GroupBy(x => x.TemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, ct);

        return templates.Select(t => new TrainingQuizTemplateListItemDto(
            t.Id,
            t.Title,
            t.Description,
            t.Category,
            t.Status,
            t.PassThreshold,
            t.AllowMultipleAttempts,
            t.CatalogItemId,
            questionCounts.GetValueOrDefault(t.Id),
            usageCounts.GetValueOrDefault(t.Id),
            t.CreatedAt,
            t.UpdatedAt,
            t.PublishedAt,
            t.ArchivedAt)).ToList();
    }

    public async Task<TrainingQuizTemplateDto?> GetTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await db.TrainingQuizTemplates.AsNoTracking()
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;
        var usage = await db.TrainingQuizzes.AsNoTracking()
            .CountAsync(q => q.TemplateId == id, ct);
        return MapTemplate(template, usage);
    }

    public async Task<TrainingQuizTemplateDto> CreateTemplateAsync(
        UpsertTrainingQuizTemplateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre du modèle est obligatoire.");
        if (request.Questions.Count == 0)
            throw new InvalidOperationException("Ajoutez au moins une question.");

        var template = new TrainingQuizTemplate
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? "",
            Category = request.Category?.Trim() ?? "",
            PassThreshold = NormalizePassThreshold(request.PassThreshold),
            AllowMultipleAttempts = request.AllowMultipleAttempts,
            CatalogItemId = request.CatalogItemId,
            CreatedByUserId = request.CreatedByUserId?.Trim() ?? "",
            Status = CatalogItemStatus.Draft,
        };
        db.TrainingQuizTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        await UpsertTemplateQuestionsAsync(template, request.Questions, ct);
        await db.SaveChangesAsync(ct);
        return (await GetTemplateAsync(template.Id, ct))!;
    }

    public async Task<TrainingQuizTemplateDto> UpdateTemplateAsync(
        Guid id,
        UpsertTrainingQuizTemplateRequest request,
        CancellationToken ct)
    {
        var template = await db.TrainingQuizTemplates
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Modèle de quiz introuvable.");

        if (template.Status == CatalogItemStatus.Archived)
            throw new InvalidOperationException("Impossible de modifier un modèle archivé.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre du modèle est obligatoire.");
        if (request.Questions.Count == 0)
            throw new InvalidOperationException("Ajoutez au moins une question.");

        template.Title = request.Title.Trim();
        template.Description = request.Description?.Trim() ?? "";
        template.Category = request.Category?.Trim() ?? "";
        template.PassThreshold = NormalizePassThreshold(request.PassThreshold);
        template.AllowMultipleAttempts = request.AllowMultipleAttempts;
        template.CatalogItemId = request.CatalogItemId;
        template.UpdatedAt = DateTime.UtcNow;
        if (template.Status == CatalogItemStatus.Published)
            template.Status = CatalogItemStatus.Draft;

        await UpsertTemplateQuestionsAsync(template, request.Questions, ct);
        await db.SaveChangesAsync(ct);
        return (await GetTemplateAsync(template.Id, ct))!;
    }

    public async Task<TrainingQuizTemplateDto> PublishTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await db.TrainingQuizTemplates
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Modèle de quiz introuvable.");
        if (template.Questions.Count == 0)
            throw new InvalidOperationException("Le modèle n'a aucune question.");
        if (template.Status == CatalogItemStatus.Archived)
            throw new InvalidOperationException("Impossible de publier un modèle archivé.");

        template.Status = CatalogItemStatus.Published;
        template.PublishedAt = DateTime.UtcNow;
        template.ArchivedAt = null;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetTemplateAsync(id, ct))!;
    }

    public async Task<TrainingQuizTemplateDto> ArchiveTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await db.TrainingQuizTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Modèle de quiz introuvable.");
        template.Status = CatalogItemStatus.Archived;
        template.ArchivedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetTemplateAsync(id, ct))!;
    }

    public async Task<TrainingQuizTemplateDto> DuplicateTemplateAsync(
        Guid id,
        string actorUserId,
        CancellationToken ct)
    {
        var source = await db.TrainingQuizTemplates.AsNoTracking()
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Modèle de quiz introuvable.");

        var copy = new TrainingQuizTemplate
        {
            Title = source.Title.EndsWith(" (copie)", StringComparison.OrdinalIgnoreCase)
                ? source.Title
                : $"{source.Title} (copie)",
            Description = source.Description,
            Category = source.Category,
            PassThreshold = source.PassThreshold,
            AllowMultipleAttempts = source.AllowMultipleAttempts,
            CatalogItemId = source.CatalogItemId,
            CreatedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? source.CreatedByUserId : actorUserId.Trim(),
            Status = CatalogItemStatus.Draft,
        };
        db.TrainingQuizTemplates.Add(copy);
        await db.SaveChangesAsync(ct);

        var root = ResolveQuizImagesRoot();
        foreach (var q in source.Questions.OrderBy(x => x.SortOrder))
        {
            var newQ = new TrainingQuizTemplateQuestion
            {
                TemplateId = copy.Id,
                SortOrder = q.SortOrder,
                Type = q.Type,
                Prompt = q.Prompt,
                OptionsJson = q.OptionsJson,
                CorrectOptionIndex = q.CorrectOptionIndex,
                AllowMultiple = q.AllowMultiple,
                CorrectOptionIndexesJson = q.CorrectOptionIndexesJson,
                Points = q.Points,
                Explanation = q.Explanation,
                ImageUrl = q.ImageUrl,
            };
            if (!string.IsNullOrWhiteSpace(q.ImageStoragePath) && File.Exists(q.ImageStoragePath))
            {
                var (path, _) = CopyImageFile(q.ImageStoragePath, root, $"tpl_{copy.Id:N}_{newQ.Id:N}");
                newQ.ImageStoragePath = path;
            }
            db.TrainingQuizTemplateQuestions.Add(newQ);
        }

        await db.SaveChangesAsync(ct);
        return (await GetTemplateAsync(copy.Id, ct))!;
    }

    public async Task<TrainingQuizDto> InstantiateToSessionAsync(
        Guid sessionId,
        Guid templateId,
        string actorUserId,
        CancellationToken ct)
    {
        _ = await db.TrainingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        var template = await db.TrainingQuizTemplates
            .Include(t => t.Questions)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new InvalidOperationException("Modèle de quiz introuvable.");

        if (template.Status == CatalogItemStatus.Archived)
            throw new InvalidOperationException("Impossible d'instancier un modèle archivé.");
        if (template.Questions.Count == 0)
            throw new InvalidOperationException("Le modèle n'a aucune question.");

        var quiz = await db.TrainingQuizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct);

        if (quiz is not null)
        {
            if (quiz.Status is TrainingQuizStatus.Published
                or TrainingQuizStatus.Graded
                or TrainingQuizStatus.Validated)
            {
                throw new InvalidOperationException(
                    "Impossible de remplacer un quiz déjà publié, noté ou validé.");
            }

            foreach (var old in quiz.Questions.ToList())
            {
                TryDeleteQuizImageFile(old.ImageStoragePath);
                db.TrainingQuizQuestions.Remove(old);
            }
            quiz.Questions.Clear();
            quiz.Title = template.Title;
            quiz.PassThreshold = template.PassThreshold;
            quiz.AllowMultipleAttempts = template.AllowMultipleAttempts;
            quiz.TemplateId = template.Id;
            quiz.Status = TrainingQuizStatus.Draft;
            quiz.RejectedReason = null;
            quiz.RejectedAt = null;
            quiz.RejectedByUserId = null;
            quiz.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            quiz = new TrainingQuiz
            {
                SessionId = sessionId,
                TemplateId = template.Id,
                Title = template.Title,
                PassThreshold = template.PassThreshold,
                AllowMultipleAttempts = template.AllowMultipleAttempts,
                CreatedByUserId = ParseActorGuid(actorUserId),
                Status = TrainingQuizStatus.Draft,
            };
            db.TrainingQuizzes.Add(quiz);
            await db.SaveChangesAsync(ct);
        }

        var root = ResolveQuizImagesRoot();
        foreach (var tq in template.Questions.OrderBy(x => x.SortOrder))
        {
            var entity = new TrainingQuizQuestion
            {
                QuizId = quiz.Id,
                SortOrder = tq.SortOrder,
                Type = tq.Type,
                Prompt = tq.Prompt,
                OptionsJson = tq.OptionsJson,
                CorrectOptionIndex = tq.CorrectOptionIndex,
                AllowMultiple = tq.AllowMultiple,
                CorrectOptionIndexesJson = tq.CorrectOptionIndexesJson,
                Points = tq.Points,
                Explanation = tq.Explanation,
            };

            if (!string.IsNullOrWhiteSpace(tq.ImageStoragePath) && File.Exists(tq.ImageStoragePath))
            {
                var (path, _) = CopyImageFile(
                    tq.ImageStoragePath,
                    root,
                    $"{sessionId:N}_{entity.Id:N}");
                entity.ImageStoragePath = path;
                entity.ImageUrl = $"/api/formations/sessions/{sessionId}/quiz/questions/{entity.Id}/image";
            }
            else if (!string.IsNullOrWhiteSpace(tq.ImageUrl))
            {
                entity.ImageUrl = tq.ImageUrl;
            }

            db.TrainingQuizQuestions.Add(entity);
        }

        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapQuizDto(await LoadQuizWithQuestionsAsync(quiz.Id, ct));
    }

    public async Task<TrainingQuizTemplateDto> PromoteSessionQuizAsync(
        Guid sessionId,
        string actorUserId,
        string? title = null,
        string? description = null,
        string? category = null,
        Guid? catalogItemId = null,
        CancellationToken ct = default)
    {
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz de session introuvable.");

        if (quiz.Questions.Count == 0)
            throw new InvalidOperationException("Le quiz n'a aucune question à promouvoir.");

        var template = new TrainingQuizTemplate
        {
            Title = string.IsNullOrWhiteSpace(title) ? quiz.Title : title.Trim(),
            Description = description?.Trim() ?? "",
            Category = category?.Trim() ?? "",
            PassThreshold = quiz.PassThreshold,
            AllowMultipleAttempts = quiz.AllowMultipleAttempts,
            CatalogItemId = catalogItemId,
            CreatedByUserId = string.IsNullOrWhiteSpace(actorUserId)
                ? quiz.CreatedByUserId.ToString()
                : actorUserId.Trim(),
            Status = CatalogItemStatus.Draft,
        };
        db.TrainingQuizTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        var root = ResolveQuizImagesRoot();
        foreach (var q in quiz.Questions.OrderBy(x => x.SortOrder))
        {
            var tq = new TrainingQuizTemplateQuestion
            {
                TemplateId = template.Id,
                SortOrder = q.SortOrder,
                Type = q.Type,
                Prompt = q.Prompt,
                OptionsJson = q.OptionsJson,
                CorrectOptionIndex = q.CorrectOptionIndex,
                AllowMultiple = q.AllowMultiple,
                CorrectOptionIndexesJson = q.CorrectOptionIndexesJson,
                Points = q.Points,
                Explanation = q.Explanation,
                ImageUrl = q.ImageUrl,
            };
            if (!string.IsNullOrWhiteSpace(q.ImageStoragePath) && File.Exists(q.ImageStoragePath))
            {
                var (path, _) = CopyImageFile(q.ImageStoragePath, root, $"tpl_{template.Id:N}_{tq.Id:N}");
                tq.ImageStoragePath = path;
                tq.ImageUrl = TemplateMediaUrl(template.Id, tq.Id);
            }
            db.TrainingQuizTemplateQuestions.Add(tq);
        }

        await db.SaveChangesAsync(ct);

        // Lier le quiz session au nouveau modèle.
        var tracked = await db.TrainingQuizzes.FirstOrDefaultAsync(q => q.Id == quiz.Id, ct);
        if (tracked is not null)
        {
            tracked.TemplateId = template.Id;
            tracked.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return (await GetTemplateAsync(template.Id, ct))!;
    }

    private async Task UpsertTemplateQuestionsAsync(
        TrainingQuizTemplate template,
        IReadOnlyList<UpsertTrainingQuizQuestionItem> questions,
        CancellationToken ct)
    {
        var existingById = template.Questions.ToDictionary(q => q.Id);
        var keepIds = new HashSet<Guid>();
        var order = 0;
        foreach (var q in questions)
        {
            ValidateQuestion(q);
            var indexes = NormalizeCorrectIndexes(q);
            TrainingQuizTemplateQuestion entity;
            if (q.Id is Guid qid && existingById.TryGetValue(qid, out var existing))
            {
                entity = existing;
                keepIds.Add(qid);
            }
            else
            {
                entity = new TrainingQuizTemplateQuestion { TemplateId = template.Id };
                db.TrainingQuizTemplateQuestions.Add(entity);
            }

            entity.SortOrder = order++;
            entity.Type = q.Type;
            entity.Prompt = q.Prompt.Trim();
            entity.OptionsJson = q.Type == TrainingQuizQuestionType.Qcm
                ? JsonSerializer.Serialize(q.Options ?? Array.Empty<string>(), JsonOpts)
                : null;
            entity.AllowMultiple = q.Type == TrainingQuizQuestionType.Qcm && q.AllowMultiple;
            entity.CorrectOptionIndex = q.Type == TrainingQuizQuestionType.Qcm && !q.AllowMultiple
                ? indexes.FirstOrDefault()
                : null;
            entity.CorrectOptionIndexesJson = q.Type == TrainingQuizQuestionType.Qcm
                ? JsonSerializer.Serialize(indexes, JsonOpts)
                : null;
            entity.Points = q.Points <= 0 ? 1m : q.Points;
            entity.Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim();

            var nextUrl = string.IsNullOrWhiteSpace(q.ImageUrl) ? null : q.ImageUrl.Trim();
            var apiMediaUrl = TemplateMediaUrl(template.Id, entity.Id);
            if (nextUrl is null)
            {
                TryDeleteQuizImageFile(entity.ImageStoragePath);
                entity.ImageStoragePath = null;
                entity.ImageUrl = null;
            }
            else if (IsTemplateMediaApiUrl(template.Id, entity.Id, nextUrl))
            {
                // Conservateur : ne pas effacer le fichier uploadé si l’URL API est renvoyée.
                entity.ImageUrl = apiMediaUrl;
            }
            else if (!string.Equals(nextUrl, entity.ImageUrl, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(entity.ImageStoragePath))
                {
                    TryDeleteQuizImageFile(entity.ImageStoragePath);
                    entity.ImageStoragePath = null;
                }
                entity.ImageUrl = nextUrl;
            }
            else
            {
                entity.ImageUrl = nextUrl;
            }
        }

        foreach (var old in existingById.Values)
        {
            if (keepIds.Contains(old.Id)) continue;
            TryDeleteQuizImageFile(old.ImageStoragePath);
            db.TrainingQuizTemplateQuestions.Remove(old);
        }

        await Task.CompletedTask;
    }

    private async Task<TrainingQuiz> LoadQuizWithQuestionsAsync(Guid quizId, CancellationToken ct) =>
        await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstAsync(q => q.Id == quizId, ct);

    private static TrainingQuizDto MapQuizDto(TrainingQuiz quiz) =>
        new(
            quiz.Id,
            quiz.SessionId,
            quiz.Title,
            quiz.Status,
            quiz.Questions.OrderBy(q => q.SortOrder).Select(q => new TrainingQuizQuestionDto(
                q.Id,
                q.SortOrder,
                q.Type,
                q.Prompt,
                ParseOptions(q.OptionsJson),
                q.CorrectOptionIndex,
                q.Points,
                q.AllowMultiple,
                ResolveCorrectIndexes(q),
                q.ImageUrl,
                q.Explanation,
                InferMediaKind(q.ImageStoragePath, q.ImageUrl))).ToList(),
            quiz.RejectedReason,
            quiz.PassThreshold <= 0 ? Formation.Domain.TrainingQuizDefaults.PassThreshold : quiz.PassThreshold,
            quiz.AllowMultipleAttempts,
            quiz.TemplateId);

    private static TrainingQuizTemplateDto MapTemplate(TrainingQuizTemplate t, int usage) =>
        new(
            t.Id,
            t.Title,
            t.Description,
            t.Category,
            t.Status,
            t.PassThreshold,
            t.AllowMultipleAttempts,
            t.CatalogItemId,
            t.CreatedByUserId,
            t.CreatedAt,
            t.UpdatedAt,
            t.PublishedAt,
            t.ArchivedAt,
            usage,
            t.Questions.OrderBy(q => q.SortOrder).Select(q => new TrainingQuizTemplateQuestionDto(
                q.Id,
                q.SortOrder,
                q.Type,
                q.Prompt,
                ParseOptions(q.OptionsJson),
                q.CorrectOptionIndex,
                q.Points,
                q.AllowMultiple,
                ResolveCorrectIndexes(q),
                q.ImageUrl,
                q.Explanation,
                InferMediaKind(q.ImageStoragePath, q.ImageUrl))).ToList());

    public async Task<TrainingQuizTemplateQuestionDto> UploadTemplateQuestionMediaAsync(
        Guid templateId,
        Guid questionId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct)
    {
        var question = await db.TrainingQuizTemplateQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TemplateId == templateId, ct)
            ?? throw new InvalidOperationException("Question introuvable.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!IsAllowedQuizMediaExtension(ext))
            throw new InvalidOperationException(
                "Format non supporté (images : jpg, png, gif, webp ; vidéos : mp4, webm, ogg, mov).");

        var root = ResolveQuizImagesRoot();
        TryDeleteQuizImageFile(question.ImageStoragePath);

        var safeName = $"tpl_{templateId:N}_{questionId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var fullPath = Path.Combine(root, safeName);
        await using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        question.ImageStoragePath = fullPath;
        question.ImageUrl = TemplateMediaUrl(templateId, questionId);

        var template = await db.TrainingQuizTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is not null) template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new TrainingQuizTemplateQuestionDto(
            question.Id,
            question.SortOrder,
            question.Type,
            question.Prompt,
            ParseOptions(question.OptionsJson),
            question.CorrectOptionIndex,
            question.Points,
            question.AllowMultiple,
            ResolveCorrectIndexes(question),
            question.ImageUrl,
            question.Explanation,
            InferMediaKind(question.ImageStoragePath, question.ImageUrl));
    }

    public async Task<(TrainingQuizTemplateQuestion Question, byte[] Bytes, string ContentType)?> GetTemplateQuestionMediaAsync(
        Guid templateId,
        Guid questionId,
        CancellationToken ct)
    {
        var question = await db.TrainingQuizTemplateQuestions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TemplateId == templateId, ct);
        if (question is null
            || string.IsNullOrWhiteSpace(question.ImageStoragePath)
            || !File.Exists(question.ImageStoragePath))
            return null;
        var bytes = await File.ReadAllBytesAsync(question.ImageStoragePath, ct);
        return (question, bytes, ContentTypeFromPath(question.ImageStoragePath));
    }

    private string ResolveQuizImagesRoot()
    {
        var configured = configuration["Formation:QuizImages:RootPath"];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "quiz-images")
            : configured;
        Directory.CreateDirectory(root);
        return root;
    }

    private static string TemplateMediaUrl(Guid templateId, Guid questionId) =>
        $"/api/formations/quiz-templates/{templateId}/questions/{questionId}/media";

    private static bool IsTemplateMediaApiUrl(Guid templateId, Guid questionId, string url) =>
        url.Contains($"/quiz-templates/{templateId}/questions/{questionId}/media", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedQuizMediaExtension(string ext) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp"
            or ".mp4" or ".webm" or ".ogg" or ".mov";

    private static string ContentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mov" => "video/quicktime",
            _ => "image/jpeg",
        };
    }

    private static string? InferMediaKind(string? storagePath, string? imageUrl)
    {
        var ext = Path.GetExtension(storagePath ?? "").ToLowerInvariant();
        if (ext is ".mp4" or ".webm" or ".ogg" or ".mov") return "video";
        if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp") return "image";
        var url = (imageUrl ?? "").ToLowerInvariant();
        if (url.Contains(".mp4") || url.Contains(".webm") || url.Contains(".ogg") || url.Contains(".mov"))
            return "video";
        if (!string.IsNullOrWhiteSpace(imageUrl)) return "image";
        return null;
    }

    private static (string FullPath, string Ext) CopyImageFile(string sourcePath, string root, string namePrefix)
    {
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var safeName = $"{namePrefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var dest = Path.Combine(root, safeName);
        File.Copy(sourcePath, dest, overwrite: true);
        return (dest, ext);
    }

    private static void TryDeleteQuizImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { File.Delete(path); } catch { /* ignore */ }
    }

    private static Guid ParseActorGuid(string actorUserId) =>
        Guid.TryParse(actorUserId, out var id) ? id : Guid.Empty;

    private static decimal NormalizePassThreshold(decimal value)
    {
        if (value <= 0) return Formation.Domain.TrainingQuizDefaults.PassThreshold;
        if (value > 100) return 100m;
        return Math.Round(value, 1);
    }

    private static void ValidateQuestion(UpsertTrainingQuizQuestionItem q)
    {
        if (string.IsNullOrWhiteSpace(q.Prompt))
            throw new InvalidOperationException("L'énoncé de la question est obligatoire.");
        if (q.Type != TrainingQuizQuestionType.Qcm) return;

        if (q.Options is null || q.Options.Count < 2)
            throw new InvalidOperationException("Un QCM nécessite au moins 2 options.");

        var indexes = NormalizeCorrectIndexes(q);
        if (indexes.Count == 0)
            throw new InvalidOperationException("Indiquez au moins une bonne réponse QCM.");
        if (indexes.Any(i => i < 0 || i >= q.Options.Count))
            throw new InvalidOperationException("Index de bonne réponse QCM invalide.");
        if (!q.AllowMultiple && indexes.Count != 1)
            throw new InvalidOperationException("Un QCM simple n'accepte qu'une seule bonne réponse.");
    }

    private static List<int> NormalizeCorrectIndexes(UpsertTrainingQuizQuestionItem q)
    {
        if (q.AllowMultiple)
        {
            return (q.CorrectOptionIndexes ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(i => i)
                .ToList();
        }

        if (q.CorrectOptionIndex is int idx)
            return [idx];
        if (q.CorrectOptionIndexes is { Count: > 0 })
            return [q.CorrectOptionIndexes[0]];
        return [];
    }

    private static List<int> ResolveCorrectIndexes(TrainingQuizQuestion q)
    {
        var fromJson = ParseIntList(q.CorrectOptionIndexesJson);
        if (fromJson.Count > 0) return fromJson;
        if (q.CorrectOptionIndex is int idx) return [idx];
        return [];
    }

    private static List<int> ResolveCorrectIndexes(TrainingQuizTemplateQuestion q)
    {
        var fromJson = ParseIntList(q.CorrectOptionIndexesJson);
        if (fromJson.Count > 0) return fromJson;
        if (q.CorrectOptionIndex is int idx) return [idx];
        return [];
    }

    private static IReadOnlyList<string>? ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static List<int> ParseIntList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }
}
