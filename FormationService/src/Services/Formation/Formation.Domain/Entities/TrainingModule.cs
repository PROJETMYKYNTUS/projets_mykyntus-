namespace Formation.Domain.Entities;

public class TrainingModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public TrainingCatalogItem? CatalogItem { get; set; }
    public ICollection<TrainingLesson> Lessons { get; set; } = new List<TrainingLesson>();
}
