using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TrainingSessionType Type { get; set; } = TrainingSessionType.Continue;
    public AnimatorKind AnimatorKind { get; set; }
    public Guid? AnimatorUserId { get; set; }
    public string? ExternalAnimatorName { get; set; }
    public string? ExternalAnimatorOrganization { get; set; }
    public string? ExternalAnimatorEmail { get; set; }
    public string? ExternalAnimatorPhone { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public int Capacity { get; set; }
    public TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Draft;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingAssignment> Assignments { get; set; } = new List<TrainingAssignment>();
}
