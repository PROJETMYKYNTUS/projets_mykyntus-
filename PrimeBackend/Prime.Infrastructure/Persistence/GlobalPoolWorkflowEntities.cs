using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence;

/// <summary>Validation effectuée d'une étape pool global (brouillon historique ou synthèse par périmètre).</summary>
public class GlobalPoolApprovalEntity
{
    public Guid Id { get; set; }
    public Guid? DraftId { get; set; }
    public SupervisorCellulePrimeDraft? Draft { get; set; }
    public Guid? ScopeSynthesisId { get; set; }
    public GlobalPoolScopeSynthesisEntity? ScopeSynthesis { get; set; }
    public Guid StepId { get; set; }
    public GlobalPoolWorkflowStep Step { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
}
