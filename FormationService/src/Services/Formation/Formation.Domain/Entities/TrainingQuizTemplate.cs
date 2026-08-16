using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingQuizTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal PassThreshold { get; set; } = global::Formation.Domain.TrainingQuizDefaults.PassThreshold;
    public bool AllowMultipleAttempts { get; set; }
    public CatalogItemStatus Status { get; set; } = CatalogItemStatus.Draft;
    public Guid? CatalogItemId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public TrainingCatalogItem? CatalogItem { get; set; }
    public ICollection<TrainingQuizTemplateQuestion> Questions { get; set; } = new List<TrainingQuizTemplateQuestion>();
}
