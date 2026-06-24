namespace EmployeeDirectory.Domain.Entities;

public class IamPermission
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public bool IsAllowed { get; set; } = true;
}
