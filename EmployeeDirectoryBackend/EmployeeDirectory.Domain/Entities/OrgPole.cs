namespace EmployeeDirectory.Domain.Entities;

public class OrgPole
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? BusinessDepartmentId { get; set; }
    public BusinessDepartment? BusinessDepartment { get; set; }
    public ICollection<OrgCellule> Cellules { get; set; } = new List<OrgCellule>();
}
