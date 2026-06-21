namespace EmployeeDirectory.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? PoleId { get; set; }
    public string? CelluleId { get; set; }
    public string? ServiceId { get; set; }
    public Guid? BusinessDepartmentId { get; set; }
    public BusinessDepartment? BusinessDepartment { get; set; }
    public Guid? AuthSubjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime HireDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [0];
}
