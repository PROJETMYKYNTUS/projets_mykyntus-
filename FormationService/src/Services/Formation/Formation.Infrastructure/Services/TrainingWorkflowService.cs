using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Services;

public sealed class TrainingWorkflowService(
    FormationDbContext db,
    IPublishEndpoint publish,
    ILogger<TrainingWorkflowService> logger)
{
    /// <summary>Seuil de réussite du quiz de formation initiale (note sur 100).</summary>
    public const decimal QuizPassThreshold = 70m;

    public async Task<TrainingSessionDto> CreateSessionAsync(CreateTrainingSessionRequest request, CancellationToken ct)
    {
        ValidateSessionAnimator(request);
        if (request.Capacity < 1)
            throw new InvalidOperationException("La capacité doit être au moins 1.");

        var plannedStart = ToUtc(request.PlannedStart);
        var plannedEnd = ToUtc(request.PlannedEnd);
        if (plannedEnd <= plannedStart)
            throw new InvalidOperationException("La date de fin doit être postérieure au début.");

        var session = new TrainingSession
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Type = TrainingSessionType.Continue,
            AnimatorKind = request.AnimatorKind,
            AnimatorUserId = request.AnimatorKind == AnimatorKind.Internal ? request.AnimatorUserId : null,
            ExternalAnimatorName = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorName?.Trim() : null,
            ExternalAnimatorOrganization = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorOrganization?.Trim() : null,
            ExternalAnimatorEmail = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorEmail?.Trim() : null,
            ExternalAnimatorPhone = request.AnimatorKind == AnimatorKind.External ? request.ExternalAnimatorPhone?.Trim() : null,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            Capacity = request.Capacity,
            Status = request.Publish ? TrainingSessionStatus.Scheduled : TrainingSessionStatus.Draft,
            CreatedByUserId = request.CreatedByUserId,
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return ToSessionDto(session, 0);
    }

    public async Task<TrainingSessionDto?> UpdateSessionStatusAsync(Guid id, TrainingSessionStatus status, CancellationToken ct)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return null;
        session.Status = status;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var count = await db.TrainingAssignments.CountAsync(a => a.SessionId == id, ct);
        return ToSessionDto(session, count);
    }

    public async Task<IReadOnlyList<TrainingSessionDto>> ListSessionsAsync(CancellationToken ct)
    {
        var sessions = await db.TrainingSessions.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        var counts = await db.TrainingAssignments.AsNoTracking()
            .GroupBy(a => a.SessionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return sessions.Select(s => ToSessionDto(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<IReadOnlyList<TrainingSessionDto>> ListAnimatedSessionsAsync(Guid animatorUserId, CancellationToken ct)
    {
        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.AnimatorKind == AnimatorKind.Internal && s.AnimatorUserId == animatorUserId)
            .OrderByDescending(s => s.PlannedStart)
            .ToListAsync(ct);
        var counts = await db.TrainingAssignments.AsNoTracking()
            .Where(a => sessions.Select(s => s.Id).Contains(a.SessionId))
            .GroupBy(a => a.SessionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return sessions.Select(s => ToSessionDto(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<IReadOnlyList<TrainingAssignmentDto>> AssignEmployeesAsync(
        Guid sessionId,
        AssignTrainingEmployeesRequest request,
        CancellationToken ct)
    {
        var session = await db.TrainingSessions.Include(s => s.Assignments).FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session introuvable.");

        var current = session.Assignments.Count;
        var incoming = request.Employees.Count;
        if (current + incoming > session.Capacity)
            throw new InvalidOperationException($"Capacité dépassée ({session.Capacity} max).");

        var newlyAssigned = new List<(Guid EmployeeId, string EmployeeName)>();
        foreach (var item in request.Employees)
        {
            if (session.Assignments.Any(a => a.EmployeeId == item.EmployeeId))
                continue;
            db.TrainingAssignments.Add(new TrainingAssignment
            {
                SessionId = sessionId,
                EmployeeId = item.EmployeeId,
                EmployeeName = item.EmployeeName.Trim(),
            });
            newlyAssigned.Add((item.EmployeeId, item.EmployeeName.Trim()));
        }

        await db.SaveChangesAsync(ct);

        foreach (var (employeeId, employeeName) in newlyAssigned)
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

        return await db.TrainingAssignments.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .Select(a => new TrainingAssignmentDto(a.Id, a.SessionId, a.EmployeeId, a.EmployeeName, a.Status))
            .ToListAsync(ct);
    }

    public async Task<InitialTrainingPathDto> CreateInitialPathAsync(CreateInitialTrainingPathRequest request, CancellationToken ct)
    {
        var existing = await db.InitialTrainingPaths
            .FirstOrDefaultAsync(p => p.EmployeeId == request.EmployeeId && p.Status != InitialTrainingStatus.Rejete && p.Status != InitialTrainingStatus.EnProduction, ct);
        if (existing is not null)
            return ToInitialDto(existing);

        var path = new InitialTrainingPath
        {
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName.Trim(),
            DateDebut = request.DateDebut,
            DateFinPrevue = request.DateFinPrevue,
            Status = InitialTrainingStatus.EnCours,
        };
        db.InitialTrainingPaths.Add(path);
        await db.SaveChangesAsync(ct);
        return ToInitialDto(path);
    }

    public async Task<IReadOnlyList<InitialTrainingPathDto>> ListInitialForFormateurAsync(CancellationToken ct)
    {
        var rows = await db.InitialTrainingPaths.AsNoTracking()
            .Where(p => p.Status != InitialTrainingStatus.EnProduction && p.Status != InitialTrainingStatus.Rejete)
            .OrderBy(p => p.DateFinPrevue)
            .ToListAsync(ct);
        return rows.Select(ToInitialDto).ToList();
    }

    public async Task<IReadOnlyList<InitialTrainingPathDto>> ListInitialPendingRhAsync(CancellationToken ct)
    {
        var rows = await db.InitialTrainingPaths.AsNoTracking()
            .Where(p => p.Status == InitialTrainingStatus.AttenteValidationRh)
            .OrderBy(p => p.FormateurValidatedAt)
            .ToListAsync(ct);
        return rows.Select(ToInitialDto).ToList();
    }

    public async Task<IReadOnlyList<InitialTrainingPathDto>> ListInitialByEmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        var rows = await db.InitialTrainingPaths.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToInitialDto).ToList();
    }

    public async Task<IReadOnlyList<InitialTrainingPathDto>> ListInitialOverviewAsync(CancellationToken ct)
    {
        var rows = await db.InitialTrainingPaths.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(500)
            .ToListAsync(ct);
        return rows.Select(ToInitialDto).ToList();
    }

    public async Task<InitialTrainingPathDto?> RecordQuizAsync(Guid id, RecordInitialQuizRequest request, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        if (request.QuizScore is < 0 or > 100)
            throw new InvalidOperationException("La note du quiz doit être entre 0 et 100.");

        var passed = request.QuizScore >= QuizPassThreshold;
        path.QuizScore = request.QuizScore;
        path.QuizPassed = passed;
        path.QuizRecordedBy = request.RecordedBy;
        path.FormateurComment = request.FormateurComment?.Trim();
        path.Status = passed
            ? InitialTrainingStatus.AttenteValidationFormateur
            : InitialTrainingStatus.QuizASaisir;
        path.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToInitialDto(path);
    }

    public async Task<InitialTrainingPathDto?> FormateurValidateAsync(Guid id, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        if (path.QuizScore is null)
            throw new InvalidOperationException("Saisissez d'abord le résultat du quiz.");
        if (path.QuizPassed != true)
            throw new InvalidOperationException(
                $"Le quiz doit être réussi (seuil {QuizPassThreshold:0} %) avant validation formateur.");
        path.Status = InitialTrainingStatus.AttenteValidationRh;
        path.FormateurValidatedAt = DateTime.UtcNow;
        path.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToInitialDto(path);
    }

    public async Task<InitialTrainingPathDto?> FormateurRejectAsync(Guid id, RejectInitialTrainingRequest request, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        return await RejectPathAsync(path, request.RejectedBy, request.Reason, ct);
    }

    public async Task<InitialTrainingPathDto?> RhValidateAsync(Guid id, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        if (path.Status != InitialTrainingStatus.AttenteValidationRh)
            throw new InvalidOperationException("Le formateur doit valider avant la RH.");
        if (path.QuizPassed != true)
            throw new InvalidOperationException(
                $"Le quiz doit être réussi (seuil {QuizPassThreshold:0} %) avant le passage en production.");

        path.Status = InitialTrainingStatus.EnProduction;
        path.RhValidatedAt = DateTime.UtcNow;
        path.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var completedAt = path.RhValidatedAt ?? DateTime.UtcNow;
        try
        {
            await publish.Publish(new InitialTrainingCompletedMessage
            {
                TrainingPathId = path.Id,
                EmployeeId = path.EmployeeId,
                EmployeeName = path.EmployeeName,
                CompletedAt = completedAt,
                ProductionStartDate = DateOnly.FromDateTime(completedAt),
                NiveauExpertiseMetier = 1,
                QuizScore = path.QuizScore,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Publication InitialTrainingCompletedMessage échouée pour employé {EmployeeId} (parcours {PathId}).",
                path.EmployeeId,
                path.Id);
        }

        return ToInitialDto(path);
    }

    public async Task<InitialTrainingPathDto?> RhRejectAsync(Guid id, RejectInitialTrainingRequest request, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        return await RejectPathAsync(path, request.RejectedBy, request.Reason, ct);
    }

    public async Task<InitialTrainingPathDto?> ExtendInitialAsync(Guid id, ExtendInitialTrainingRequest request, CancellationToken ct)
    {
        var path = await db.InitialTrainingPaths.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (path is null) return null;
        path.DateFinPrevue = request.DateFinPrevue;
        path.QuizScore = null;
        path.QuizPassed = null;
        path.FormateurValidatedAt = null;
        path.Status = InitialTrainingStatus.QuizASaisir;
        path.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToInitialDto(path);
    }

    private async Task<InitialTrainingPathDto> RejectPathAsync(
        InitialTrainingPath path,
        string rejectedBy,
        string reason,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Le motif de rejet est obligatoire.");
        path.Status = InitialTrainingStatus.Rejete;
        path.RejectedBy = rejectedBy.Trim();
        path.RejectReason = reason.Trim();
        path.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            await publish.Publish(new InitialTrainingRejectedMessage
            {
                TrainingPathId = path.Id,
                EmployeeId = path.EmployeeId,
                EmployeeName = path.EmployeeName,
                RejectedBy = path.RejectedBy ?? rejectedBy.Trim(),
                Reason = path.RejectReason ?? reason.Trim(),
                RejectedAt = DateTime.UtcNow,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Publication InitialTrainingRejectedMessage échouée pour employé {EmployeeId} (parcours {PathId}).",
                path.EmployeeId,
                path.Id);
        }

        return ToInitialDto(path);
    }

    private static void ValidateSessionAnimator(CreateTrainingSessionRequest request)
    {
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

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static TrainingSessionDto ToSessionDto(TrainingSession session, int assignmentCount) =>
        new(
            session.Id,
            session.Title,
            session.Description,
            session.Type,
            session.AnimatorKind,
            session.AnimatorUserId,
            session.ExternalAnimatorName,
            session.ExternalAnimatorOrganization,
            session.ExternalAnimatorEmail,
            session.ExternalAnimatorPhone,
            session.PlannedStart,
            session.PlannedEnd,
            session.Capacity,
            session.Status,
            assignmentCount);

    private static InitialTrainingPathDto ToInitialDto(InitialTrainingPath path) =>
        new(
            path.Id,
            path.EmployeeId,
            path.EmployeeName,
            path.DateDebut,
            path.DateFinPrevue,
            path.Status,
            path.QuizScore is not null,
            path.FormateurValidatedAt,
            path.RhValidatedAt,
            path.RejectedBy,
            path.RejectReason);
}
