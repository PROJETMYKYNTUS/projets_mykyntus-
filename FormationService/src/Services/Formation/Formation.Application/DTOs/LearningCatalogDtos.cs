using Formation.Domain.Enums;

namespace Formation.Application.DTOs;

public sealed record TrainingCatalogItemDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    CatalogItemStatus Status,
    bool IsActive,
    LearningGateMode DefaultGateMode,
    CatalogAudienceMatchMode AudienceMatchMode,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt,
    int ModuleCount,
    int LessonCount,
    int ResourceCount,
    TrainingCatalogAudienceDto? Audience = null,
    IReadOnlyList<TrainingModuleDto>? Modules = null,
    bool SelfServiceEnabled = false,
    CatalogDueMode DueMode = CatalogDueMode.None,
    DateTime? DueDate = null,
    int? DueInDays = null,
    Guid? DefaultQuizTemplateId = null);

public sealed record TrainingCatalogAudienceDto(
    CatalogAudienceMatchMode MatchMode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> StructureKeys,
    IReadOnlyList<Guid> UserIds,
    int EstimatedBeneficiaryCount = 0);

public sealed record TrainingModuleDto(
    Guid Id,
    Guid CatalogItemId,
    string Title,
    string Description,
    int SortOrder,
    IReadOnlyList<TrainingLessonDto> Lessons);

public sealed record TrainingLessonDto(
    Guid Id,
    Guid ModuleId,
    string Title,
    string Description,
    int SortOrder,
    bool IsRequired,
    IReadOnlyList<TrainingResourceDto> Resources,
    bool IsCompleted = false,
    decimal ProgressPercent = 0m);

public sealed record TrainingResourceDto(
    Guid Id,
    Guid LessonId,
    TrainingResourceType Type,
    string Title,
    string? Url,
    string? ContentType,
    string? FileName,
    string? TextContent,
    int SortOrder,
    int? DurationMinutes,
    string? DownloadPath = null);

public sealed class UpsertTrainingCatalogItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public LearningGateMode DefaultGateMode { get; set; } = LearningGateMode.Content;
    public CatalogAudienceMatchMode AudienceMatchMode { get; set; } = CatalogAudienceMatchMode.MatchAny;
    public string CreatedByUserId { get; set; } = string.Empty;
    public bool SelfServiceEnabled { get; set; }
    public CatalogDueMode DueMode { get; set; } = CatalogDueMode.None;
    public DateTime? DueDate { get; set; }
    public int? DueInDays { get; set; }
    public Guid? DefaultQuizTemplateId { get; set; }
}

public sealed class UpsertTrainingCatalogAudienceRequest
{
    public CatalogAudienceMatchMode MatchMode { get; set; } = CatalogAudienceMatchMode.MatchAny;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> StructureKeys { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Guid> UserIds { get; set; } = Array.Empty<Guid>();
}

public sealed class UpsertTrainingModuleRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class UpsertTrainingLessonRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;
}

public sealed class UpsertTrainingResourceRequest
{
    public TrainingResourceType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? TextContent { get; set; }
    public int SortOrder { get; set; }
    public int? DurationMinutes { get; set; }
}

/// <summary>Remplacement atomique de l'arbre modules → leçons → ressources (hors fichiers en attente d'upload).</summary>
public sealed class ReplaceCatalogStructureRequest
{
    public IReadOnlyList<StructureModuleRequest> Modules { get; set; } = Array.Empty<StructureModuleRequest>();
}

public sealed class StructureModuleRequest
{
    public string ClientKey { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public IReadOnlyList<StructureLessonRequest> Lessons { get; set; } = Array.Empty<StructureLessonRequest>();
}

public sealed class StructureLessonRequest
{
    public string ClientKey { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public IReadOnlyList<StructureResourceRequest> Resources { get; set; } = Array.Empty<StructureResourceRequest>();
}

public sealed class StructureResourceRequest
{
    public string ClientKey { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public TrainingResourceType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? TextContent { get; set; }
    public int SortOrder { get; set; }
    public int? DurationMinutes { get; set; }
}

public sealed record ReplaceCatalogStructureResponse(
    Guid CatalogItemId,
    IReadOnlyList<StructureModuleResultDto> Modules);

public sealed record StructureModuleResultDto(
    string ClientKey,
    Guid Id,
    IReadOnlyList<StructureLessonResultDto> Lessons);

public sealed record StructureLessonResultDto(
    string ClientKey,
    Guid Id,
    IReadOnlyList<StructureResourceResultDto> Resources);

public sealed record StructureResourceResultDto(
    string ClientKey,
    Guid Id);

public sealed record ResourceAccessTokenDto(
    string Url,
    DateTime ExpiresAt);

public sealed class LinkSessionCatalogRequest
{
    public Guid? CatalogItemId { get; set; }
    public LearningGateMode? LearningGateMode { get; set; }
    public bool AssignAudience { get; set; } = true;
    public Guid ActorUserId { get; set; }
}

public sealed class CompleteLessonRequest
{
    public Guid EmployeeId { get; set; }
    public Guid? LastResourceId { get; set; }
}

public sealed record CatalogPlayerDto(
    Guid CatalogItemId,
    Guid? SessionId,
    Guid? AssignmentId,
    Guid EnrollmentId,
    string Title,
    string Description,
    string Category,
    LearningGateMode GateMode,
    decimal ProgressPercent,
    int RequiredLessonsTotal,
    int RequiredLessonsDone,
    bool CanTakeQuiz,
    string? QuizBlockedReason,
    IReadOnlyList<TrainingModuleDto> Modules,
    DateTime? DueAt = null,
    CatalogEnrollmentStatus EnrollmentStatus = CatalogEnrollmentStatus.NotStarted,
    Guid? DefaultQuizTemplateId = null);

public sealed record MySelfServiceCatalogItemDto(
    Guid CatalogItemId,
    string Title,
    string Description,
    string Category,
    Guid EnrollmentId,
    CatalogEnrollmentStatus Status,
    DateTime? DueAt,
    decimal ProgressPercent,
    int RequiredLessonsTotal,
    int RequiredLessonsDone,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record LearningQuizStatsDto(
    int CatalogCount,
    int SessionWithCatalogCount,
    int QuestionCount,
    int AttemptCount,
    double AvgScore,
    double BestScore,
    double PassRate,
    IReadOnlyList<LearningQuizStatsBySessionDto> BySession);

public sealed record LearningQuizStatsBySessionDto(
    Guid SessionId,
    Guid? CatalogItemId,
    string Title,
    string? Category,
    int QuestionCount,
    int AttemptCount,
    double AvgScore,
    double BestScore,
    double PassRate);

public sealed record LearningQuizResultExportRowDto(
    string EmployeeName,
    string Email,
    string Role,
    string StructureKey,
    Guid? SessionId,
    string SessionTitle,
    Guid? CatalogItemId,
    decimal? Score,
    bool? Passed,
    int AttemptNumber,
    DateTime SubmittedAt);
