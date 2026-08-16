namespace Formation.Domain.Entities;

public class TrainingLessonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Guid LessonId { get; set; }
    public Guid? LastResourceId { get; set; }
    public decimal ProgressPercent { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public TrainingCatalogEnrollment? Enrollment { get; set; }
    public TrainingLesson? Lesson { get; set; }
}
