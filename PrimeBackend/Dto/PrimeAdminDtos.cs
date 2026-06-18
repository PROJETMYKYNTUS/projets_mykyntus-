namespace PrimeBackend.Dto;

// =====================================================================
// Phase 1.3 / 1.4 : DTOs Administration PRIME
// =====================================================================

public sealed class RbacPermissionDto
{
    public Guid Id { get; init; }
    public string Role { get; init; } = "";
    public string Action { get; init; } = "";
    public string Scope { get; init; } = "";
    public bool IsAllowed { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class UpsertRbacPermissionRequest
{
    public string Role { get; set; } = "";
    public string Action { get; set; } = "";
    public string Scope { get; set; } = "Global";
    public bool IsAllowed { get; set; }
}

public sealed class WorkflowStepConfigDto
{
    public Guid Id { get; init; }
    public int SortOrder { get; init; }
    public string ApproverRole { get; init; } = "";
    public string FromStatus { get; init; } = "";
    public string ToStatus { get; init; } = "";
    public bool IsActive { get; init; }
    public int SlaHours { get; init; }
    public bool CapturesAmountsOnApproval { get; init; }
    public bool TerminalApproved { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class UpsertWorkflowStepConfigRequest
{
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SlaHours { get; set; }
    public bool CapturesAmountsOnApproval { get; set; }
    public bool TerminalApproved { get; set; }
}

public sealed class WorkflowGlobalConfigDto
{
    public Guid Id { get; init; }
    public bool NotificationsEnabled { get; init; }
    public int GlobalSlaHours { get; init; }
    public bool AllowBulkApprove { get; init; }
    public bool RequireRejectReason { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class UpdateWorkflowGlobalConfigRequest
{
    public bool NotificationsEnabled { get; set; } = true;
    public int GlobalSlaHours { get; set; } = 72;
    public bool AllowBulkApprove { get; set; } = true;
    public bool RequireRejectReason { get; set; } = true;
}

public sealed class RecordAuditNavigationRequest
{
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string Route { get; set; } = "";
}

public sealed class AuditLogDto
{
    public Guid Id { get; init; }
    public DateTimeOffset At { get; init; }
    public string UserId { get; init; } = "";
    public string UserDisplayName { get; init; } = "";
    public string Role { get; init; } = "";
    public string Action { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string? EntityId { get; init; }
    public string? DetailJson { get; init; }
    public string? IpAddress { get; init; }
}

public sealed class AnomalyDto
{
    public Guid Id { get; init; }
    public DateTimeOffset DetectedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string Type { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Status { get; init; } = "";
    public string Description { get; init; } = "";
    public string? TargetEntityType { get; init; }
    public string? TargetEntityId { get; init; }
    public string? Period { get; init; }
    public string? ServiceId { get; init; }
    public string? CelluleId { get; init; }
    public string? PoleId { get; init; }
    public string? ContextJson { get; init; }
    public string? ResolvedByUserId { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
}

public sealed class UpdateAnomalyStatusBody
{
    public string Status { get; set; } = "";
    public string? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}
