namespace EmployeeDirectory.Domain.Entities;

public class BusinessDepartment
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BusinessDepartmentKind Kind { get; set; } = BusinessDepartmentKind.Operational;
    public Guid? ManagerEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<DepartmentPoleAssignment> PoleAssignments { get; set; } = new List<DepartmentPoleAssignment>();
}
