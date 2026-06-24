namespace Prime.Domain.Entities;

/// <summary>Étape du workflow de validation des fiches PRIME (configurable).</summary>
public class WorkflowStepConfig
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SlaHours { get; set; }
    public bool CapturesAmountsOnApproval { get; set; }
    public bool TerminalApproved { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Configuration globale du workflow (singleton).</summary>
public class WorkflowGlobalConfig
{
    public Guid Id { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int GlobalSlaHours { get; set; } = 72;
    public bool AllowBulkApprove { get; set; } = true;
    public bool RequireRejectReason { get; set; } = true;
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Journal d'audit des actions structurelles et de validation.</summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>Anomalie détectée sur une fiche PRIME.</summary>
public class Anomaly
{
    public Guid Id { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string Description { get; set; } = "";
    public string? TargetEntityType { get; set; }
    public string? TargetEntityId { get; set; }
    public string? Period { get; set; }
    public string? ServiceId { get; set; }
    public string? CelluleId { get; set; }
    public string? PoleId { get; set; }
    public string? ContextJson { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}

/// <summary>Étape du workflow « fichier global des primes ».</summary>
public class GlobalPoolWorkflowStep
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
