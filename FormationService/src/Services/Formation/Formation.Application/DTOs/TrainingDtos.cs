using Formation.Domain.Enums;

namespace Formation.Application.DTOs;

public sealed record TrainingSessionDto(
    Guid Id,
    string Title,
    string Description,
    TrainingSessionType Type,
    AnimatorKind AnimatorKind,
    Guid? AnimatorUserId,
    string? ExternalAnimatorName,
    string? ExternalAnimatorOrganization,
    string? ExternalAnimatorEmail,
    string? ExternalAnimatorPhone,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    int Capacity,
    TrainingSessionStatus Status,
    int AssignmentCount);

public sealed record TrainingAssignmentDto(
    Guid Id,
    Guid SessionId,
    Guid EmployeeId,
    string EmployeeName,
    TrainingAssignmentStatus Status,
    string Attendance);

/// <summary>Session continue où l'employé est bénéficiaire (Mes formations).</summary>
public sealed record MyAssignedTrainingSessionDto(
    Guid SessionId,
    Guid AssignmentId,
    string Title,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    TrainingSessionStatus Status,
    string Attendance);

public sealed record InitialTrainingPathDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateTime DateDebut,
    DateTime DateFinPrevue,
    InitialTrainingStatus Status,
    bool HasQuizResult,
    DateTime? FormateurValidatedAt,
    DateTime? RhValidatedAt,
    string? RejectedBy,
    string? RejectReason);

public sealed class MarkTrainingAttendanceRequest
{
    /// <summary>Present | Absent</summary>
    public string Attendance { get; set; } = string.Empty;
    public Guid AnimatorUserId { get; set; }
}

public sealed class CreateTrainingSessionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnimatorKind AnimatorKind { get; set; }
    public Guid? AnimatorUserId { get; set; }
    public string? ExternalAnimatorName { get; set; }
    public string? ExternalAnimatorOrganization { get; set; }
    public string? ExternalAnimatorEmail { get; set; }
    public string? ExternalAnimatorPhone { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public int Capacity { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public bool Publish { get; set; }
}

public sealed class AssignTrainingEmployeesRequest
{
    public IReadOnlyList<AssignTrainingEmployeeItem> Employees { get; set; } = Array.Empty<AssignTrainingEmployeeItem>();
}

public sealed class AssignTrainingEmployeeItem
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public sealed class CreateInitialTrainingPathRequest
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime DateDebut { get; set; }
    public DateTime DateFinPrevue { get; set; }
}

public sealed class RecordInitialQuizRequest
{
    public decimal QuizScore { get; set; }
    public bool QuizPassed { get; set; }
    public string? FormateurComment { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class RejectInitialTrainingRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class ExtendInitialTrainingRequest
{
    public DateTime DateFinPrevue { get; set; }
}
