using System.Text.Json;
using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Formation.Infrastructure.Services;

public sealed partial class TrainingWorkflowService
{
    public const decimal ContinueQuizPassThreshold = 70m;

    public async Task<TrainingProgramDto> CreateProgramAsync(CreateTrainingProgramRequest request, CancellationToken ct)
    {
        ValidateProgramAnimator(request);
        if (request.Capacity < 1)
            throw new InvalidOperationException("La capacité doit être au moins 1.");

        var mode = request.Mode;
        var sessionCount = mode == TrainingProgramMode.Single ? 1 : request.SessionCount;
        if (sessionCount < 1)
            throw new InvalidOperationException("Le nombre de séances doit être au moins 1.");
        if (request.Sessions.Count != sessionCount)
            throw new InvalidOperationException($"Fournissez exactement {sessionCount} créneau(x) de séance.");

        var program = new TrainingProgram
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Mode = mode,
            SessionCount = sessionCount,
            AnimatorKind = request.AnimatorKind,
            AnimatorUserId = request.AnimatorKind == AnimatorKind.Internal ? request.AnimatorUserId : null,
            ExternalAnimatorName = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorName?.Trim() : null,
            ExternalAnimatorOrganization = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorOrganization?.Trim() : null,
            ExternalAnimatorEmail = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorEmail?.Trim() : null,
            ExternalAnimatorPhone = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorPhone?.Trim() : null,
            Capacity = request.Capacity,
            CreatedByUserId = request.CreatedByUserId,
        };
        db.TrainingPrograms.Add(program);

        var sessions = new List<TrainingSession>();
        for (var i = 0; i < sessionCount; i++)
        {
            var slot = request.Sessions[i];
            var plannedStart = ToUtc(slot.PlannedStart);
            var plannedEnd = ToUtc(slot.PlannedEnd);
            if (plannedEnd <= plannedStart)
                throw new InvalidOperationException($"Séance {i + 1} : la fin doit être postérieure au début.");

            var session = new TrainingSession
            {
                ProgramId = program.Id,
                SequenceNumber = i + 1,
                Title = sessionCount == 1 ? program.Title : $"{program.Title} — séance {i + 1}",
                Description = program.Description,
                Type = TrainingSessionType.Continue,
                AnimatorKind = program.AnimatorKind,
                AnimatorUserId = program.AnimatorUserId,
                ExternalAnimatorName = program.ExternalAnimatorName,
                ExternalAnimatorOrganization = program.ExternalAnimatorOrganization,
                ExternalAnimatorEmail = program.ExternalAnimatorEmail,
                ExternalAnimatorPhone = program.ExternalAnimatorPhone,
                PlannedStart = plannedStart,
                PlannedEnd = plannedEnd,
                Capacity = program.Capacity,
                Status = request.Publish
                    ? ResolveStatusFromSchedule(plannedStart, plannedEnd, DateTime.UtcNow)
                    : TrainingSessionStatus.Draft,
                CreatedByUserId = request.CreatedByUserId,
            };
            db.TrainingSessions.Add(session);
            sessions.Add(session);
        }

        await db.SaveChangesAsync(ct);

        if (request.Publish
            && program.AnimatorKind == AnimatorKind.Internal
            && program.AnimatorUserId is Guid animatorId
            && animatorId != Guid.Empty)
        {
            foreach (var session in sessions)
            {
                await publish.Publish(new Kyntus.Messaging.Contracts.TrainingSessionAnimatorAssignedMessage
                {
                    SessionId = session.Id,
                    Title = session.Title,
                    PlannedStart = session.PlannedStart,
                    PlannedEnd = session.PlannedEnd,
                    AnimatorUserId = animatorId,
                    AssignedAt = DateTime.UtcNow,
                }, ct);
            }
        }

        return new TrainingProgramDto(
            program.Id,
            program.Title,
            program.Description,
            program.Mode,
            program.SessionCount,
            program.AnimatorKind,
            program.AnimatorUserId,
            program.ExternalAnimatorName,
            program.Capacity,
            sessions.Select(s => ToSessionDto(s, 0)).ToList());
    }

    public async Task<IReadOnlyList<TrainingAssignmentDto>> AssignEmployeesToProgramAsync(
        Guid programId,
        AssignTrainingEmployeesRequest request,
        CancellationToken ct)
    {
        var sessions = await db.TrainingSessions
            .Include(s => s.Assignments)
            .Where(s => s.ProgramId == programId)
            .ToListAsync(ct);
        if (sessions.Count == 0)
            throw new InvalidOperationException("Programme introuvable ou sans séances.");

        var allAssigned = new List<TrainingAssignmentDto>();
        foreach (var session in sessions)
        {
            var body = new AssignTrainingEmployeesRequest { Employees = request.Employees };
            var assigned = await AssignEmployeesAsync(session.Id, body, ct);
            allAssigned.AddRange(assigned);
        }

        return allAssigned
            .GroupBy(a => a.EmployeeId)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<TrainingSessionReportDto> UploadSessionReportAsync(
        Guid sessionId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        Stream content,
        string? reportsRootPath,
        CancellationToken ct)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        var isAnimator = session.AnimatorKind == AnimatorKind.Internal && session.AnimatorUserId == uploadedByUserId;
        if (!isAnimator)
            throw new InvalidOperationException("Seul l'animateur de la séance peut déposer le compte rendu.");

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };
        if (!allowed.Contains(contentType)
            && !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Formats acceptés : PDF ou Word (.doc, .docx).");

        var root = reportsRootPath;
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "training-reports");
        Directory.CreateDirectory(root);

        var safeName = $"{sessionId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(root, safeName);
        await using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        var existing = await db.TrainingSessionReports.FirstOrDefaultAsync(r => r.SessionId == sessionId, ct);
        if (existing is not null)
        {
            if (File.Exists(existing.StoragePath))
            {
                try { File.Delete(existing.StoragePath); } catch { /* ignore */ }
            }
            existing.FileName = fileName;
            existing.ContentType = contentType;
            existing.StoragePath = fullPath;
            existing.UploadedByUserId = uploadedByUserId;
            existing.UploadedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new TrainingSessionReportDto(existing.Id, existing.SessionId, existing.FileName, existing.ContentType, existing.UploadedAt);
        }

        var report = new TrainingSessionReport
        {
            SessionId = sessionId,
            UploadedByUserId = uploadedByUserId,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            StoragePath = fullPath,
        };
        db.TrainingSessionReports.Add(report);
        await db.SaveChangesAsync(ct);
        return new TrainingSessionReportDto(report.Id, report.SessionId, report.FileName, report.ContentType, report.UploadedAt);
    }

    public async Task<(TrainingSessionReport Report, byte[] Bytes)?> GetSessionReportAsync(Guid sessionId, CancellationToken ct)
    {
        var report = await db.TrainingSessionReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SessionId == sessionId, ct);
        if (report is null || !File.Exists(report.StoragePath))
            return null;
        var bytes = await File.ReadAllBytesAsync(report.StoragePath, ct);
        return (report, bytes);
    }

    public async Task<TrainingQuizDto> UpsertQuizAsync(Guid sessionId, UpsertTrainingQuizRequest request, CancellationToken ct)
    {
        _ = await RequireAnimatorContinueSessionAsync(sessionId, request.AnimatorUserId, ct);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre du quiz est obligatoire.");
        if (request.Questions.Count == 0)
            throw new InvalidOperationException("Ajoutez au moins une question.");
        var threshold = NormalizePassThreshold(request.PassThreshold);

        var quiz = await db.TrainingQuizzes.Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct);
        if (quiz is null)
        {
            quiz = new TrainingQuiz
            {
                SessionId = sessionId,
                Title = request.Title.Trim(),
                PassThreshold = threshold,
                AllowMultipleAttempts = request.AllowMultipleAttempts,
                CreatedByUserId = request.AnimatorUserId,
                Status = TrainingQuizStatus.Draft,
            };
            db.TrainingQuizzes.Add(quiz);
        }
        else
        {
            if (quiz.Status is TrainingQuizStatus.Validated)
                throw new InvalidOperationException("Impossible de modifier un quiz validé.");
            quiz.Title = request.Title.Trim();
            quiz.PassThreshold = threshold;
            quiz.AllowMultipleAttempts = request.AllowMultipleAttempts;
            quiz.UpdatedAt = DateTime.UtcNow;
            quiz.Status = TrainingQuizStatus.Draft;
            quiz.RejectedReason = null;
            quiz.RejectedAt = null;
            quiz.RejectedByUserId = null;
            db.TrainingQuizQuestions.RemoveRange(quiz.Questions);
        }

        var order = 0;
        foreach (var q in request.Questions)
        {
            ValidateQuestion(q);
            var indexes = NormalizeCorrectIndexes(q);
            db.TrainingQuizQuestions.Add(new TrainingQuizQuestion
            {
                QuizId = quiz.Id,
                SortOrder = order++,
                Type = q.Type,
                Prompt = q.Prompt.Trim(),
                OptionsJson = q.Type == TrainingQuizQuestionType.Qcm
                    ? JsonSerializer.Serialize(q.Options ?? Array.Empty<string>())
                    : null,
                AllowMultiple = q.Type == TrainingQuizQuestionType.Qcm && q.AllowMultiple,
                CorrectOptionIndex = q.Type == TrainingQuizQuestionType.Qcm && !q.AllowMultiple
                    ? indexes.FirstOrDefault()
                    : null,
                CorrectOptionIndexesJson = q.Type == TrainingQuizQuestionType.Qcm
                    ? JsonSerializer.Serialize(indexes)
                    : null,
                Points = q.Points <= 0 ? 1m : q.Points,
                ImageUrl = string.IsNullOrWhiteSpace(q.ImageUrl) ? null : q.ImageUrl.Trim(),
                Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim(),
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetQuizDtoAsync(quiz.Id, ct)
            ?? throw new InvalidOperationException("Quiz introuvable après enregistrement.");
    }

    public async Task<TrainingQuizDto> PublishQuizAsync(Guid sessionId, Guid animatorUserId, CancellationToken ct)
    {
        _ = await RequireAnimatorContinueSessionAsync(sessionId, animatorUserId, ct);
        var quiz = await db.TrainingQuizzes.Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        if (quiz.Questions.Count == 0)
            throw new InvalidOperationException("Le quiz n'a aucune question.");
        quiz.Status = TrainingQuizStatus.Published;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetQuizDtoAsync(quiz.Id, ct))!;
    }

    public async Task<TrainingQuizDto?> GetQuizForSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct);
        return quiz is null ? null : await GetQuizDtoAsync(quiz.Id, ct);
    }

    public async Task<TrainingQuizForEmployeeDto> GetPublishedQuizForEmployeeAsync(
        Guid sessionId,
        Guid employeeId,
        CancellationToken ct)
    {
        var assignment = await db.TrainingAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.EmployeeId == employeeId, ct)
            ?? throw new InvalidOperationException("Vous n'êtes pas affecté à cette séance.");

        var session = await db.TrainingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        var (gateOk, reason) = await learningCatalog.EvaluateQuizGateAsync(
            session, assignment, session.CatalogItemId, ct);
        if (!gateOk)
            throw new InvalidOperationException(reason ?? "Quiz non disponible.");

        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        if (quiz.Status is not TrainingQuizStatus.Published and not TrainingQuizStatus.Graded
            and not TrainingQuizStatus.Validated)
            throw new InvalidOperationException("Le quiz n'est pas encore publié.");

        if (!quiz.AllowMultipleAttempts
            && await db.TrainingQuizAttempts.AnyAsync(a => a.QuizId == quiz.Id && a.AssignmentId == assignment.Id, ct))
            throw new InvalidOperationException("Une seule tentative autorisée pour ce quiz.");

        return new TrainingQuizForEmployeeDto(
            quiz.Id,
            quiz.SessionId,
            quiz.Title,
            quiz.Status,
            quiz.Questions.OrderBy(q => q.SortOrder).Select(q => new TrainingQuizQuestionPublicDto(
                q.Id,
                q.SortOrder,
                q.Type,
                q.Prompt,
                ParseOptions(q.OptionsJson),
                q.Points,
                q.AllowMultiple,
                q.ImageUrl)).ToList(),
            quiz.AllowMultipleAttempts,
            quiz.PassThreshold <= 0 ? ContinueQuizPassThreshold : quiz.PassThreshold);
    }

    public async Task<TrainingQuizAttemptDto> SubmitQuizAttemptAsync(
        Guid sessionId,
        SubmitTrainingQuizAttemptRequest request,
        CancellationToken ct)
    {
        var quiz = await db.TrainingQuizzes.Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        if (quiz.Status != TrainingQuizStatus.Published)
            throw new InvalidOperationException("Le quiz n'accepte plus de nouvelles réponses.");

        var assignment = await db.TrainingAssignments
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId && a.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Affectation introuvable.");
        if (assignment.EmployeeId != request.EmployeeId)
            throw new InvalidOperationException("Employé incohérent avec l'affectation.");

        var session = await db.TrainingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");
        var (gateOk, reason) = await learningCatalog.EvaluateQuizGateAsync(
            session, assignment, session.CatalogItemId, ct);
        if (!gateOk)
            throw new InvalidOperationException(reason ?? "Quiz non disponible.");

        var existingCount = await db.TrainingQuizAttempts
            .CountAsync(a => a.QuizId == quiz.Id && a.AssignmentId == assignment.Id, ct);
        if (!quiz.AllowMultipleAttempts && existingCount > 0)
            throw new InvalidOperationException("Une tentative existe déjà pour ce quiz.");

        var payload = request.Answers.Select(a => new
        {
            a.QuestionId,
            a.SelectedOptionIndex,
            SelectedOptionIndexes = ResolveSelectedIndexes(a),
            a.FreeText,
        });

        var attempt = new TrainingQuizAttempt
        {
            QuizId = quiz.Id,
            AssignmentId = assignment.Id,
            EmployeeId = request.EmployeeId,
            AttemptNumber = existingCount + 1,
            AnswersJson = JsonSerializer.Serialize(payload),
        };

        ApplyAttemptScoring(attempt, quiz.Questions.ToList(), quiz.PassThreshold);

        db.TrainingQuizAttempts.Add(attempt);
        if (attempt.IsGraded)
            quiz.Status = TrainingQuizStatus.Graded;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToAttemptDto(attempt, assignment.EmployeeName, quiz.Questions.ToList());
    }

    public async Task<IReadOnlyList<TrainingQuizAttemptDto>> ListMyQuizAttemptsAsync(
        Guid sessionId,
        Guid employeeId,
        CancellationToken ct)
    {
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        var assignment = await db.TrainingAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.EmployeeId == employeeId, ct)
            ?? throw new InvalidOperationException("Affectation introuvable.");

        var attempts = await db.TrainingQuizAttempts.AsNoTracking()
            .Where(a => a.QuizId == quiz.Id && a.AssignmentId == assignment.Id)
            .OrderByDescending(a => a.AttemptNumber)
            .ToListAsync(ct);
        var questions = quiz.Questions.OrderBy(q => q.SortOrder).ToList();
        return attempts.Select(a => ToAttemptDto(a, assignment.EmployeeName, questions)).ToList();
    }

    public async Task<TrainingQuizAttemptDto> GradeFreeTextAnswerAsync(
        Guid sessionId,
        Guid attemptId,
        GradeFreeTextAnswerRequest request,
        CancellationToken ct)
    {
        _ = await RequireAnimatorContinueSessionAsync(sessionId, request.AnimatorUserId, ct);
        var quiz = await db.TrainingQuizzes.Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        var attempt = await db.TrainingQuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.QuizId == quiz.Id, ct)
            ?? throw new InvalidOperationException("Tentative introuvable.");

        var question = quiz.Questions.FirstOrDefault(q => q.Id == request.QuestionId)
            ?? throw new InvalidOperationException("Question introuvable.");
        if (question.Type != TrainingQuizQuestionType.FreeText)
            throw new InvalidOperationException("Seules les réponses libres se notent Correct / Fausse.");

        var grades = ParseFreeTextGrades(attempt.FreeTextGradesJson);
        grades[request.QuestionId] = request.IsCorrect;
        attempt.FreeTextGradesJson = JsonSerializer.Serialize(
            grades.ToDictionary(kv => kv.Key.ToString("D"), kv => kv.Value));
        attempt.GradedByUserId = request.AnimatorUserId;

        ApplyAttemptScoring(attempt, quiz.Questions.ToList(), quiz.PassThreshold);
        if (attempt.IsGraded)
            quiz.Status = TrainingQuizStatus.Graded;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var name = await db.TrainingAssignments.AsNoTracking()
            .Where(a => a.Id == attempt.AssignmentId)
            .Select(a => a.EmployeeName)
            .FirstOrDefaultAsync(ct) ?? "";
        return ToAttemptDto(attempt, name, quiz.Questions.OrderBy(q => q.SortOrder).ToList());
    }

    public async Task<IReadOnlyList<TrainingQuizAttemptDto>> ListQuizAttemptsAsync(
        Guid sessionId,
        Guid animatorUserId,
        CancellationToken ct)
    {
        _ = await RequireAnimatorContinueSessionAsync(sessionId, animatorUserId, ct);
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");

        var attempts = await db.TrainingQuizAttempts.AsNoTracking()
            .Where(a => a.QuizId == quiz.Id)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);
        var assignmentIds = attempts.Select(a => a.AssignmentId).ToList();
        var names = await db.TrainingAssignments.AsNoTracking()
            .Where(a => assignmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.EmployeeName, ct);

        var questions = quiz.Questions.OrderBy(q => q.SortOrder).ToList();
        return attempts
            .Select(a => ToAttemptDto(a, names.GetValueOrDefault(a.AssignmentId, ""), questions))
            .ToList();
    }

    public async Task<TrainingQuizAttemptDto> GradeQuizAttemptAsync(
        Guid sessionId,
        Guid attemptId,
        GradeTrainingQuizAttemptRequest request,
        CancellationToken ct)
    {
        _ = await RequireAnimatorContinueSessionAsync(sessionId, request.AnimatorUserId, ct);
        var quiz = await db.TrainingQuizzes.Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        var attempt = await db.TrainingQuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.QuizId == quiz.Id, ct)
            ?? throw new InvalidOperationException("Tentative introuvable.");

        attempt.GradedByUserId = request.AnimatorUserId;
        attempt.AnimatorComment = request.AnimatorComment?.Trim();
        ApplyAttemptScoring(attempt, quiz.Questions.ToList(), quiz.PassThreshold);
        if (!attempt.IsGraded && request.ManualScore is decimal manual)
        {
            attempt.ManualScore = manual;
            attempt.FinalScore = manual;
            attempt.Passed = manual >= NormalizePassThreshold(quiz.PassThreshold);
            attempt.IsGraded = true;
            attempt.GradedAt = DateTime.UtcNow;
        }

        if (attempt.IsGraded)
            quiz.Status = TrainingQuizStatus.Graded;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var name = await db.TrainingAssignments.AsNoTracking()
            .Where(a => a.Id == attempt.AssignmentId)
            .Select(a => a.EmployeeName)
            .FirstOrDefaultAsync(ct) ?? "";

        return ToAttemptDto(attempt, name, quiz.Questions.OrderBy(q => q.SortOrder).ToList());
    }

    public async Task<TrainingQuizDto> ValidateQuizAsync(Guid sessionId, ValidateTrainingQuizRequest request, CancellationToken ct)
    {
        var quiz = await db.TrainingQuizzes.FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        if (quiz.Status is not TrainingQuizStatus.Graded and not TrainingQuizStatus.Published)
            throw new InvalidOperationException("Le quiz doit être noté avant validation.");
        quiz.Status = TrainingQuizStatus.Validated;
        quiz.ValidatedByUserId = request.ActorUserId;
        quiz.ValidatedAt = DateTime.UtcNow;
        quiz.RejectedReason = null;
        quiz.RejectedAt = null;
        quiz.RejectedByUserId = null;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetQuizDtoAsync(quiz.Id, ct))!;
    }

    public async Task<TrainingQuizDto> RejectQuizAsync(Guid sessionId, RejectTrainingQuizRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Le motif de rejet est obligatoire.");
        var quiz = await db.TrainingQuizzes.FirstOrDefaultAsync(q => q.SessionId == sessionId, ct)
            ?? throw new InvalidOperationException("Quiz introuvable.");
        quiz.Status = TrainingQuizStatus.Rejected;
        quiz.RejectedByUserId = request.ActorUserId;
        quiz.RejectedAt = DateTime.UtcNow;
        quiz.RejectedReason = request.Reason.Trim();
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetQuizDtoAsync(quiz.Id, ct))!;
    }

    public async Task<FormationDashboardStatsDto> GetDashboardStatsAsync(
        IReadOnlyCollection<Guid>? employeeScope,
        CancellationToken ct)
    {
        await SyncPublishedSessionStatusesAsync(ct);
        var now = DateTime.UtcNow;
        var scope = employeeScope is { Count: > 0 }
            ? employeeScope.Where(id => id != Guid.Empty).ToHashSet()
            : null;

        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.Type == TrainingSessionType.Continue)
            .ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var assignments = await db.TrainingAssignments.AsNoTracking()
            .Where(a => sessionIds.Contains(a.SessionId))
            .ToListAsync(ct);

        if (scope is not null)
        {
            assignments = assignments.Where(a => scope.Contains(a.EmployeeId)).ToList();
            var scopedSessionIds = assignments.Select(a => a.SessionId).ToHashSet();
            // Inclure aussi les séances à venir du périmètre (même sans affectation encore) :
            // on restreint aux séances ayant au moins un bénéficiaire du scope.
            sessions = sessions.Where(s => scopedSessionIds.Contains(s.Id)).ToList();
            sessionIds = sessions.Select(s => s.Id).ToList();
        }

        var programIds = sessions
            .Where(s => s.ProgramId is Guid)
            .Select(s => s.ProgramId!.Value)
            .Distinct()
            .ToHashSet();
        var programCount = scope is null
            ? await db.TrainingPrograms.CountAsync(ct)
            : programIds.Count;

        var present = assignments.Count(a => a.Status == TrainingAssignmentStatus.Completed);
        var assignmentCount = assignments.Count;
        var attendanceRate = assignmentCount == 0 ? 0 : Math.Round(100.0 * present / assignmentCount, 1);

        var quizzes = await db.TrainingQuizzes.AsNoTracking()
            .Where(q => sessionIds.Contains(q.SessionId))
            .ToListAsync(ct);
        var attemptsQuery = db.TrainingQuizAttempts.AsNoTracking()
            .Where(a => quizzes.Select(q => q.Id).Contains(a.QuizId) && a.IsGraded);
        if (scope is not null)
            attemptsQuery = attemptsQuery.Where(a => scope.Contains(a.EmployeeId));
        var attempts = await attemptsQuery.ToListAsync(ct);
        var passed = attempts.Count(a => a.Passed == true);
        var quizSuccess = attempts.Count == 0 ? 0 : Math.Round(100.0 * passed / attempts.Count, 1);

        var reportSessionIds = await db.TrainingSessionReports.AsNoTracking()
            .Where(r => sessionIds.Contains(r.SessionId))
            .Select(r => r.SessionId)
            .ToListAsync(ct);
        var missingReports = sessions.Count(s =>
            s.Status == TrainingSessionStatus.Completed && !reportSessionIds.Contains(s.Id));

        return new FormationDashboardStatsDto(
            programCount,
            sessions.Count,
            assignmentCount,
            present,
            attendanceRate,
            quizzes.Count,
            quizzes.Count(q => q.Status == TrainingQuizStatus.Validated),
            attempts.Count,
            passed,
            quizSuccess,
            sessions.Count(s => s.Status == TrainingSessionStatus.Scheduled && s.PlannedStart >= now),
            missingReports,
            quizzes.Count(q => q.Status == TrainingQuizStatus.Graded));
    }

    private async Task<TrainingQuizDto?> GetQuizDtoAsync(Guid quizId, CancellationToken ct)
    {
        var quiz = await db.TrainingQuizzes.AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz is null) return null;
        return new TrainingQuizDto(
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
                q.Explanation)).ToList(),
            quiz.RejectedReason,
            quiz.PassThreshold <= 0 ? ContinueQuizPassThreshold : quiz.PassThreshold,
            quiz.AllowMultipleAttempts);
    }

    private static void ValidateProgramAnimator(CreateTrainingProgramRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("L'intitulé est obligatoire.");
        if (request.AnimatorKind == AnimatorKind.Internal && request.AnimatorUserId is null)
            throw new InvalidOperationException("Sélectionnez un animateur interne.");
        if (request.AnimatorKind == AnimatorKind.External)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalAnimatorName))
                throw new InvalidOperationException("Le nom de l'animateur externe est obligatoire.");
            if (string.IsNullOrWhiteSpace(request.ExternalAnimatorEmail))
                throw new InvalidOperationException("L'email de l'animateur externe est obligatoire.");
        }
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

    private static List<int> ResolveSelectedIndexes(TrainingQuizAnswerItem? ans)
    {
        if (ans is null) return [];
        if (ans.SelectedOptionIndexes is { Count: > 0 })
            return ans.SelectedOptionIndexes.Distinct().OrderBy(i => i).ToList();
        if (ans.SelectedOptionIndex is int idx) return [idx];
        return [];
    }

    private static bool SetsEqual(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count != b.Count) return false;
        return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
    }

    private static TrainingQuizAttemptDto ToAttemptDto(
        TrainingQuizAttempt attempt,
        string employeeName,
        IReadOnlyList<TrainingQuizQuestion> questions)
    {
        return new TrainingQuizAttemptDto(
            attempt.Id,
            attempt.QuizId,
            attempt.AssignmentId,
            attempt.EmployeeId,
            employeeName,
            attempt.AutoScore,
            attempt.ManualScore,
            attempt.FinalScore,
            attempt.Passed,
            attempt.IsGraded,
            attempt.SubmittedAt,
            attempt.AnimatorComment,
            BuildAnswerDetails(attempt.AnswersJson, questions, attempt.FreeTextGradesJson),
            attempt.AttemptNumber);
    }

    private static void ApplyAttemptScoring(
        TrainingQuizAttempt attempt,
        IReadOnlyList<TrainingQuizQuestion> questions,
        decimal passThreshold)
    {
        var threshold = NormalizePassThreshold(passThreshold);
        var answers = ParseStoredAnswers(attempt.AnswersJson);
        var freeTextGrades = ParseFreeTextGrades(attempt.FreeTextGradesJson);

        decimal totalPoints = 0;
        decimal earnedPoints = 0;
        var freeTextIds = questions.Where(q => q.Type == TrainingQuizQuestionType.FreeText).Select(q => q.Id).ToList();
        var allFreeTextGraded = freeTextIds.Count == 0 || freeTextIds.All(id => freeTextGrades.ContainsKey(id));

        foreach (var question in questions)
        {
            totalPoints += question.Points;
            if (question.Type == TrainingQuizQuestionType.Qcm)
            {
                answers.TryGetValue(question.Id, out var ans);
                var selected = ans?.Selected ?? [];
                if (SetsEqual(ResolveCorrectIndexes(question), selected))
                    earnedPoints += question.Points;
            }
            else if (freeTextGrades.TryGetValue(question.Id, out var ok) && ok)
            {
                earnedPoints += question.Points;
            }
        }

        var percent = totalPoints <= 0 ? 100m : Math.Round(earnedPoints / totalPoints * 100m, 2);
        attempt.AutoScore = percent;
        attempt.FinalScore = percent;
        attempt.ManualScore = null;
        attempt.Passed = allFreeTextGraded ? percent >= threshold : null;
        attempt.IsGraded = allFreeTextGraded;
        if (allFreeTextGraded)
            attempt.GradedAt ??= DateTime.UtcNow;
        else
            attempt.GradedAt = null;
    }

    private static decimal NormalizePassThreshold(decimal value)
    {
        if (value <= 0) return ContinueQuizPassThreshold;
        if (value > 100) return 100m;
        return Math.Round(value, 1);
    }

    private static Dictionary<Guid, bool> ParseFreeTextGrades(string? json)
    {
        var result = new Dictionary<Guid, bool>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (map is null) return result;
            foreach (var (k, v) in map)
            {
                if (Guid.TryParse(k, out var id))
                    result[id] = v;
            }
        }
        catch
        {
            // ignore malformed
        }
        return result;
    }

    private static IReadOnlyList<TrainingQuizAttemptAnswerDetailDto> BuildAnswerDetails(
        string answersJson,
        IReadOnlyList<TrainingQuizQuestion> questions,
        string? freeTextGradesJson = null)
    {
        var parsed = ParseStoredAnswers(answersJson);
        var freeTextGrades = ParseFreeTextGrades(freeTextGradesJson);
        return questions.OrderBy(q => q.SortOrder).Select(q =>
        {
            parsed.TryGetValue(q.Id, out var ans);
            var options = ParseOptions(q.OptionsJson);
            var correct = ResolveCorrectIndexes(q);
            var selected = ans?.Selected ?? [];
            bool? isCorrect = q.Type == TrainingQuizQuestionType.Qcm
                ? SetsEqual(correct, selected)
                : freeTextGrades.TryGetValue(q.Id, out var graded) ? graded : null;
            return new TrainingQuizAttemptAnswerDetailDto(
                q.Id,
                q.SortOrder,
                q.Type,
                q.Prompt,
                options,
                selected.Count == 1 ? selected[0] : null,
                selected,
                ans?.FreeText,
                correct.Count == 1 ? correct[0] : null,
                correct,
                q.AllowMultiple,
                isCorrect,
                q.Points,
                q.ImageUrl,
                q.Explanation);
        }).ToList();
    }

    private sealed record StoredAnswer(List<int> Selected, string? FreeText);

    private static Dictionary<Guid, StoredAnswer> ParseStoredAnswers(string? json)
    {
        var result = new Dictionary<Guid, StoredAnswer>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("QuestionId", out var idEl)
                    && !el.TryGetProperty("questionId", out idEl))
                    continue;
                if (!Guid.TryParse(idEl.GetString(), out var qid)) continue;

                var selected = new List<int>();
                if (el.TryGetProperty("SelectedOptionIndexes", out var multi)
                    || el.TryGetProperty("selectedOptionIndexes", out multi))
                {
                    if (multi.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var i in multi.EnumerateArray())
                            if (i.TryGetInt32(out var v)) selected.Add(v);
                    }
                }
                else if (el.TryGetProperty("SelectedOptionIndex", out var single)
                         || el.TryGetProperty("selectedOptionIndex", out single))
                {
                    if (single.ValueKind == JsonValueKind.Number && single.TryGetInt32(out var v))
                        selected.Add(v);
                }

                string? free = null;
                if (el.TryGetProperty("FreeText", out var ft) || el.TryGetProperty("freeText", out ft))
                    free = ft.GetString();

                result[qid] = new StoredAnswer(selected.Distinct().OrderBy(x => x).ToList(), free);
            }
        }
        catch
        {
            // ignore malformed payload
        }

        return result;
    }

    private static IReadOnlyList<string>? ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static List<int> ParseIntList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json)?.Distinct().OrderBy(i => i).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
