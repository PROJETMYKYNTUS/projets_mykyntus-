using System.ComponentModel.DataAnnotations;
using Planning.Domain.Enums;

namespace Planning.Domain.Entities;

public class PlanningChangeRequest
{
    public int Id { get; set; }

    [MaxLength(16)]
    public string WeekCode { get; set; } = string.Empty;

    public int RequesterUserId { get; set; }
    public User Requester { get; set; } = null!;

    public int CurrentAssignmentId { get; set; }
    public ShiftAssignment CurrentAssignment { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public int? ProposedSwapUserId { get; set; }
    public User? ProposedSwapUser { get; set; }

    public PlanningChangeRequestStatus Status { get; set; } = PlanningChangeRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ProcessedByUserId { get; set; }
    public User? ProcessedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}
