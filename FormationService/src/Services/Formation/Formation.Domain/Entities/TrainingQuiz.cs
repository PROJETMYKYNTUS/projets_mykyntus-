using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingQuiz
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Seuil de réussite en % (score final ≥ plafond → Valide).</summary>
    public decimal PassThreshold { get; set; } = 70m;
    /// <summary>Si false, une seule tentative par affectation (comportement historique).</summary>
    public bool AllowMultipleAttempts { get; set; }
    public TrainingQuizStatus Status { get; set; } = TrainingQuizStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ValidatedByUserId { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedReason { get; set; }

    public TrainingSession? Session { get; set; }
    public ICollection<TrainingQuizQuestion> Questions { get; set; } = new List<TrainingQuizQuestion>();
    public ICollection<TrainingQuizAttempt> Attempts { get; set; } = new List<TrainingQuizAttempt>();
}
