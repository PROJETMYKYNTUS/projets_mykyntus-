namespace Planning.Application.DTOs.Planning;

public class CreatePlanningChangeRequestDto
{
    public int CurrentAssignmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Obligatoire : id du collègue pour le switch.</summary>
    public int ProposedSwapUserId { get; set; }
}

public class RejectPlanningChangeRequestDto
{
    public string? Reason { get; set; }
}

public class PlanningChangeRequestDto
{
    public int Id { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public int RequesterUserId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public int CurrentAssignmentId { get; set; }
    public string AssignmentDay { get; set; } = string.Empty;
    public DateOnly AssignmentDate { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? ProposedSwapUserId { get; set; }
    public string? ProposedSwapUserName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PartnerRespondedAt { get; set; }
    public int? SupervisorProcessedByUserId { get; set; }
    public string? SupervisorProcessedByName { get; set; }
    public int? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public int WeeklyPlanningId { get; set; }
    /// <summary>True si le viewer courant est le partenaire proposé.</summary>
    public bool ViewerIsPartner { get; set; }
    /// <summary>True si le viewer courant est le demandeur.</summary>
    public bool ViewerIsRequester { get; set; }
}

public class SwapCandidateDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int AssignmentId { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
}

public class ChangeRequestStatsByEmployeeDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
}
