using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingQuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public int SortOrder { get; set; }
    public TrainingQuizQuestionType Type { get; set; }
    public string Prompt { get; set; } = string.Empty;
    /// <summary>JSON array of options for QCM, e.g. ["A","B","C"].</summary>
    public string? OptionsJson { get; set; }
    /// <summary>Index of correct option for single-answer QCM (0-based), or null for free text / multi.</summary>
    public int? CorrectOptionIndex { get; set; }
    /// <summary>When true, several options may be correct (QCM multi-choix).</summary>
    public bool AllowMultiple { get; set; }
    /// <summary>JSON array of correct option indexes for multi QCM, e.g. [0,2].</summary>
    public string? CorrectOptionIndexesJson { get; set; }
    public decimal Points { get; set; } = 1m;

    public TrainingQuiz? Quiz { get; set; }
}
