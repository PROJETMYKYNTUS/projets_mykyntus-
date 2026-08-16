using Formation.Domain;
using Formation.Domain.Enums;

namespace Formation.Application.DTOs;

public sealed record TrainingQuizTemplateListItemDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    CatalogItemStatus Status,
    decimal PassThreshold,
    bool AllowMultipleAttempts,
    Guid? CatalogItemId,
    int QuestionCount,
    int SessionUsageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt);

public sealed record TrainingQuizTemplateQuestionDto(
    Guid Id,
    int SortOrder,
    TrainingQuizQuestionType Type,
    string Prompt,
    IReadOnlyList<string>? Options,
    int? CorrectOptionIndex,
    decimal Points,
    bool AllowMultiple = false,
    IReadOnlyList<int>? CorrectOptionIndexes = null,
    string? ImageUrl = null,
    string? Explanation = null,
    /// <summary>image | video — déduit du fichier uploadé ou de l'URL.</summary>
    string? MediaKind = null);

public sealed record TrainingQuizTemplateDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    CatalogItemStatus Status,
    decimal PassThreshold,
    bool AllowMultipleAttempts,
    Guid? CatalogItemId,
    string CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt,
    int SessionUsageCount,
    IReadOnlyList<TrainingQuizTemplateQuestionDto> Questions);

public sealed class UpsertTrainingQuizTemplateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal PassThreshold { get; set; } = TrainingQuizDefaults.PassThreshold;
    public bool AllowMultipleAttempts { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public IReadOnlyList<UpsertTrainingQuizQuestionItem> Questions { get; set; } = Array.Empty<UpsertTrainingQuizQuestionItem>();
}

public sealed class InstantiateQuizTemplateRequest
{
    public Guid SessionId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class PromoteSessionQuizRequest
{
    public Guid SessionId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? CatalogItemId { get; set; }
}
