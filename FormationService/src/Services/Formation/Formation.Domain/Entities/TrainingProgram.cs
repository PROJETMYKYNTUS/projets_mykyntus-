using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

/// <summary>Série de formation continue (une ou plusieurs séances).</summary>
public class TrainingProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TrainingProgramMode Mode { get; set; } = TrainingProgramMode.Single;
    public int SessionCount { get; set; } = 1;
    public AnimatorKind AnimatorKind { get; set; }
    public Guid? AnimatorUserId { get; set; }
    public string? ExternalAnimatorName { get; set; }
    public string? ExternalAnimatorOrganization { get; set; }
    public string? ExternalAnimatorEmail { get; set; }
    public string? ExternalAnimatorPhone { get; set; }
    public int Capacity { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingSession> Sessions { get; set; } = new List<TrainingSession>();
}
