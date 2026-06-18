namespace EmployeeDirectory.Domain.Entities;

public class OrgCellule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PoleId { get; set; } = string.Empty;
    public OrgPole Pole { get; set; } = null!;
    public ICollection<OrgService> Services { get; set; } = new List<OrgService>();
}
