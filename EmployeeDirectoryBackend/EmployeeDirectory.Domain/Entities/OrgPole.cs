namespace EmployeeDirectory.Domain.Entities;

public class OrgPole
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<OrgCellule> Cellules { get; set; } = new List<OrgCellule>();
}
