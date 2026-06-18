namespace PrimeBackend.Data;

/// <summary>Étape configurable du workflow « fichier global des primes » (validations parallèles = même <see cref="SortOrder"/>).</summary>
public class GlobalPoolWorkflowStepEntity
{
    public Guid Id { get; set; }
    /// <summary>Ordre de vague : toutes les étapes actives avec le même tri doivent être validées avant la vague suivante.</summary>
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Validation effectuée d’une étape pool global (brouillon historique ou synthèse par périmètre).</summary>
public class GlobalPoolApprovalEntity
{
    public Guid Id { get; set; }
    public Guid? DraftId { get; set; }
    public SupervisorCellulePrimeDraftEntity? Draft { get; set; }
    public Guid? ScopeSynthesisId { get; set; }
    public GlobalPoolScopeSynthesisEntity? ScopeSynthesis { get; set; }
    public Guid StepId { get; set; }
    public GlobalPoolWorkflowStepEntity Step { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
}
