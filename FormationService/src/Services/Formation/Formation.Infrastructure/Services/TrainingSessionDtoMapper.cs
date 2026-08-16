using Formation.Application.DTOs;
using Formation.Domain.Entities;
using Formation.Domain.Enums;

namespace Formation.Infrastructure.Services;

/// <summary>
/// Mapping unique TrainingSession → TrainingSessionDto (évite les reconstructions manuelles divergentes).
/// </summary>
public static class TrainingSessionDtoMapper
{
    public static TrainingSessionDto ToDto(
        TrainingSession session,
        int assignmentCount,
        bool hasReport = false,
        Guid? quizId = null,
        string? quizStatus = null,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        ResolveAttendanceGate(session, now, out var canMark, out var attendanceBlocked);
        ResolveReportGate(session, now, out var canUpload, out var reportBlocked);

        return new TrainingSessionDto(
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
            assignmentCount,
            session.ProgramId,
            session.SequenceNumber,
            hasReport,
            quizId,
            quizStatus,
            session.CatalogItemId,
            session.LearningGateMode?.ToString(),
            canMark,
            canUpload,
            attendanceBlocked,
            reportBlocked);
    }

    public static void ResolveAttendanceGate(
        TrainingSession session,
        DateTime utcNow,
        out bool canMarkAttendance,
        out string? attendanceBlockedReason)
    {
        if (session.Status is TrainingSessionStatus.Draft or TrainingSessionStatus.Cancelled)
        {
            canMarkAttendance = false;
            attendanceBlockedReason = "Impossible de pointer les présences sur une session brouillon ou annulée.";
            return;
        }

        if (utcNow < session.PlannedStart)
        {
            canMarkAttendance = false;
            attendanceBlockedReason = "Le pointage n’est possible qu’à partir du début de la séance.";
            return;
        }

        if (session.Status is not TrainingSessionStatus.InProgress
            and not TrainingSessionStatus.Completed
            and not TrainingSessionStatus.Scheduled)
        {
            canMarkAttendance = false;
            attendanceBlockedReason = "Le pointage n’est pas autorisé pour ce statut de session.";
            return;
        }

        canMarkAttendance = true;
        attendanceBlockedReason = null;
    }

    public static void ResolveReportGate(
        TrainingSession session,
        DateTime utcNow,
        out bool canUploadReport,
        out string? reportBlockedReason)
    {
        if (utcNow < session.PlannedEnd)
        {
            canUploadReport = false;
            reportBlockedReason = "Le dépôt du compte rendu n’est possible qu’après la fin de la séance.";
            return;
        }

        canUploadReport = true;
        reportBlockedReason = null;
    }
}
