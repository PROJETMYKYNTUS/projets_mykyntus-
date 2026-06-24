namespace EmployeeDirectory.Domain.Entities;

public class OrgService
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CelluleId { get; set; } = string.Empty;
    public OrgCellule Cellule { get; set; } = null!;
}
