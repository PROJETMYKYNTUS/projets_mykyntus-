namespace Formation.Domain.Entities;

public class TrainingLesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;

    public TrainingModule? Module { get; set; }
    public ICollection<TrainingResource> Resources { get; set; } = new List<TrainingResource>();
}
