using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingCatalogEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogItemId { get; set; }
    public Guid EmployeeId { get; set; }
    public CatalogEnrollmentSource Source { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? AssignmentId { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public CatalogEnrollmentStatus Status { get; set; } = CatalogEnrollmentStatus.NotStarted;
    public DateTime? LastReminderAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TrainingCatalogItem? CatalogItem { get; set; }
    public ICollection<TrainingLessonProgress> LessonProgresses { get; set; } = new List<TrainingLessonProgress>();
}
