using System.ComponentModel.DataAnnotations;
using Planning.Domain.Enums;

namespace Planning.Domain.Entities;

/// <summary>
/// Demande exceptionnelle pré-génération : 1 employé + 1 jour + 1 shift template.
/// </summary>
public class PlanningExceptionalRequest
{
    public int Id { get; set; }

    [MaxLength(16)]
    public string WeekCode { get; set; } = string.Empty;

    public DateOnly RequestedDate { get; set; }

    public int RequesterUserId { get; set; }
    public User Requester { get; set; } = null!;

    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    /// <summary>FK vers SubServiceShiftConfig template (IsTemplate=true).</summary>
    public int RequestedShiftTemplateId { get; set; }
    public SubServiceShiftConfig RequestedShiftTemplate { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public PlanningExceptionalRequestStatus Status { get; set; }
        = PlanningExceptionalRequestStatus.PendingSupervisor;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool JustificationRequired { get; set; }

    [MaxLength(260)]
    public string? JustificationFileName { get; set; }

    [MaxLength(128)]
    public string? JustificationContentType { get; set; }

    public byte[]? JustificationContent { get; set; }

    public int? SupervisorProcessedByUserId { get; set; }
    public User? SupervisorProcessedBy { get; set; }
    public DateTime? SupervisorProcessedAt { get; set; }

    public int? RhProcessedByUserId { get; set; }
    public User? RhProcessedBy { get; set; }
    public DateTime? RhProcessedAt { get; set; }

    public int? ProcessedByUserId { get; set; }
    public User? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}
