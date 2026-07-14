using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public TrainingSession? Session { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public TrainingAssignmentStatus Status { get; set; } = TrainingAssignmentStatus.Assigned;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
