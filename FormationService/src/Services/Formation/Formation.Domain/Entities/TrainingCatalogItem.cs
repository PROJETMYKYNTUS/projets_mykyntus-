using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

/// <summary>Formation e-learning réutilisable (catalogue), indépendante des sessions planifiées.</summary>
public class TrainingCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public CatalogItemStatus Status { get; set; } = CatalogItemStatus.Draft;
    public bool IsActive { get; set; } = true;
    public LearningGateMode DefaultGateMode { get; set; } = LearningGateMode.Content;
    public CatalogAudienceMatchMode AudienceMatchMode { get; set; } = CatalogAudienceMatchMode.MatchAny;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<TrainingModule> Modules { get; set; } = new List<TrainingModule>();
    public ICollection<TrainingCatalogAudienceRule> AudienceRules { get; set; } = new List<TrainingCatalogAudienceRule>();
}
