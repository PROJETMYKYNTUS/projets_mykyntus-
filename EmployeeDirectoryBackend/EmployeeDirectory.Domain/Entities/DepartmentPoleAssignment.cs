namespace EmployeeDirectory.Domain.Entities;

public class DepartmentPoleAssignment
{
    public Guid Id { get; set; }
    public Guid BusinessDepartmentId { get; set; }
    public BusinessDepartment BusinessDepartment { get; set; } = null!;
    public string PoleId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
