namespace PrimeBackend.Dto;

// =====================================================================
// Phase 1.2 : DTOs / Requests pour le workflow de validation
// =====================================================================

/// <summary>Requête d'approbation d'une fiche service par le valideur courant.</summary>
public sealed class ApproveServiceFicheRequest
{
    /// <summary>UserId du valideur.</summary>
    public string UserId { get; set; } = "";
    /// <summary>Rôle du valideur (Référent technique | Superviseur | Chef de projet | RH).</summary>
    public string Role { get; set; } = "";
    /// <summary>Montant prime calculé (optionnel — snapshoté à la première étape).</summary>
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
}

public sealed class RejectServiceFicheRequest
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class BulkApproveServiceFicheRequest
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public List<Guid> FicheIds { get; set; } = [];
}

/// <summary>Réponse simplifiée d'une fiche pour les écrans de validation/résultats.</summary>
public sealed class EmployeePrimeServiceFicheValidationDto
{
    public Guid Id { get; init; }
    public string EmployeeId { get; init; } = "";
    public string SupervisorUserId { get; init; } = "";
    public string ServiceId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string FillingStatus { get; init; } = "";
    public string ValidationStatus { get; init; } = "";
    public string? LastApproverUserId { get; init; }
    public DateTimeOffset? LastApprovedAt { get; init; }
    public string? RejectedByUserId { get; init; }
    public DateTimeOffset? RejectedAt { get; init; }
    public string? RejectionReason { get; init; }
    public decimal? PrimeAmount { get; init; }
    public decimal? ChallengeAmount { get; init; }
    public decimal? TotalAmount { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Récapitulatif par statut (utilisé par les dashboards Validation/Résultats).</summary>
public sealed class WorkflowStatusSummaryDto
{
    public int Pending { get; init; }
    public int ReferentTechniqueApproved { get; init; }
    public int SuperviseurApproved { get; init; }
    public int ChefDeProjetApproved { get; init; }
    public int RhApproved { get; init; }
    public int Rejected { get; init; }
    public int Total { get; init; }
}
