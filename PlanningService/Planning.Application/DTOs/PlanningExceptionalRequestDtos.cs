namespace Planning.Application.DTOs.Planning;

public class PlanningExceptionalRequestDto
{
    public int Id { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly RequestedDate { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public int? RequestedShiftTemplateId { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public string ShiftStartTime { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool JustificationRequired { get; set; }
    public bool HasJustification { get; set; }
    public string? JustificationFileName { get; set; }
    public int? SupervisorProcessedByUserId { get; set; }
    public string? SupervisorProcessedByName { get; set; }
    public DateTime? SupervisorProcessedAt { get; set; }
    public int? RhProcessedByUserId { get; set; }
    public string? RhProcessedByName { get; set; }
    public DateTime? RhProcessedAt { get; set; }
    public int? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool ViewerIsRequester { get; set; }
}

public class RejectPlanningExceptionalRequestDto
{
    public string? Reason { get; set; }
}

public class ExceptionalRequestQuotaDto
{
    public int Year { get; set; }
    public int ApprovedCount { get; set; }
    public int FreeLimit { get; set; } = 3;
    public int FreeRemaining { get; set; }
    public bool JustificationRequiredNext { get; set; }
}

public class ExceptionalShiftOptionDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int WorkHours { get; set; }
    public int DisplayOrder { get; set; }
}

public class ExceptionalRequestTargetWeekDto
{
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime DeadlineLocal { get; set; }
    public bool DeadlinePassed { get; set; }

    /// <summary>Semaines encore ouvertes au dépôt (en cours + suivante), pour choix employé.</summary>
    public List<ExceptionalRequestWeekOptionDto> AvailableWeeks { get; set; } = new();
}

public class ExceptionalRequestWeekOptionDto
{
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    /// <summary>CurrentWeek | NextWeek</summary>
    public string Kind { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
}
