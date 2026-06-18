using Kyntus.Messaging.Contracts;

namespace EmployeeDirectory.Domain.Entities;

public class OrgAssignmentHistory
{
    public Guid Id { get; set; }
    public OrgAssignmentKind Kind { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public OrgNodeLevel NodeLevel { get; set; }
    public Guid? PreviousEmployeeId { get; set; }
    public Guid? NewEmployeeId { get; set; }
    public Guid? ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
