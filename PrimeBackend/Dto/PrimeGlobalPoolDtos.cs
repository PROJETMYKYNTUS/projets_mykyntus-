namespace PrimeBackend.Dto;

public sealed class CelluleDraftGlobalPoolStateDto
{
    public Guid DraftId { get; init; }
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public bool HasFile { get; init; }
    public string? FileName { get; init; }
    public DateTimeOffset? UploadedAt { get; init; }
    public DateTimeOffset? ManagerApprovedAt { get; init; }
    public DateTimeOffset? RhApprovedAt { get; init; }
    public DateTimeOffset? ComptaAckAt { get; init; }
    public bool PoolDistributionUnlocked { get; init; }
}

/// <summary>File global PRIME — entrée de file d’attente (Manager / RH / Comptable) avec identifiant superviseur pour compat API historique.</summary>
public sealed class GlobalPoolInboxItemDto
{
    public Guid DraftId { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public bool HasFile { get; init; }
    public string? FileName { get; init; }
    public DateTimeOffset? UploadedAt { get; init; }
    public DateTimeOffset? ManagerApprovedAt { get; init; }
    public DateTimeOffset? RhApprovedAt { get; init; }
    public DateTimeOffset? ComptaAckAt { get; init; }
    public bool PoolDistributionUnlocked { get; init; }
    /// <summary>Vrai si l’utilisateur connecté a encore une action attendue sur ce brouillon.</summary>
    public bool PendingActionForUser { get; init; }
    /// <summary>Présent uniquement lorsque le workflow global configurable est actif : état par étape.</summary>
    public List<GlobalPoolInboxStepStatusDto>? StepStatuses { get; init; }
    /// <summary>Première étape que l’utilisateur (ou Admin) peut valider dans la vague courante.</summary>
    public Guid? SuggestedApproveStepId { get; init; }
}

public sealed class GlobalPoolInboxStepStatusDto
{
    public Guid StepId { get; init; }
    public int SortOrder { get; init; }
    public string ApproverRole { get; init; } = "";
    public bool IsRequired { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
}

public sealed class GlobalPoolActingUserRequest
{
    public string UserId { get; set; } = "";
}

public sealed class GlobalPoolWorkflowStepDto
{
    public Guid Id { get; init; }
    public int SortOrder { get; init; }
    public string ApproverRole { get; init; } = "";
    public bool IsRequired { get; init; }
    public bool IsActive { get; init; }
}

public sealed class GlobalPoolApproveStepRequest
{
    public string UserId { get; set; } = "";
    public Guid StepId { get; set; }
    /// <summary>Rôle revendiqué (si absent : en-tête <c>X-Prime-Role</c>).</summary>
    public string? Role { get; set; }
}

public sealed class UpsertGlobalPoolWorkflowStepRequest
{
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
