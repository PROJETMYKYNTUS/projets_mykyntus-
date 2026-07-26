namespace Formation.Domain.Entities;

public class TrainingQuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid EmployeeId { get; set; }
    /// <summary>JSON map questionId → answer (option index or free text).</summary>
    public string AnswersJson { get; set; } = "{}";
    /// <summary>JSON map questionId → bool (notation Correct/Fausse des réponses libres).</summary>
    public string? FreeTextGradesJson { get; set; }
    public decimal? AutoScore { get; set; }
    public decimal? ManualScore { get; set; }
    public decimal? FinalScore { get; set; }
    public bool? Passed { get; set; }
    public bool IsGraded { get; set; }
    public Guid? GradedByUserId { get; set; }
    public DateTime? GradedAt { get; set; }
    public string? AnimatorComment { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public TrainingQuiz? Quiz { get; set; }
}
