using EmployeeDirectory.Domain.Enums;

namespace EmployeeDirectory.Domain.Entities;

public class OrgAssignment
{
    public Guid Id { get; set; }
    public OrgAssignmentKind Kind { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public OrgNodeLevel NodeLevel { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public Guid? SupersededBy { get; set; }
    public Guid? ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
}
