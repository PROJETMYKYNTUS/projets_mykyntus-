namespace PrimeBackend.Data;

// =====================================================================
// Phase 1.3 : Entités EF pour l'Administration PRIME
// (RBAC, Workflow config, AuditLog, Anomaly)
// =====================================================================

/// <summary>
/// Permission RBAC (matrice rôle × action × scope) pour le module PRIME.
/// Chaque ligne décrit ce qu'un rôle (Admin, RH, Chef de projet, Superviseur,
/// Référent technique, Pilote, Audit) peut faire (Read/Edit/Validate/Configure)
/// et sur quel périmètre (Global / Pole / Cellule / Service / Self).
/// </summary>
public class RbacPermissionEntity
{
    public Guid Id { get; set; }
    /// <summary>Rôle métier (Admin | RH | Chef de projet | Superviseur | Référent technique | Pilote | Audit).</summary>
    public string Role { get; set; } = "";
    /// <summary>Action : Read | Edit | Validate | Configure.</summary>
    public string Action { get; set; } = "";
    /// <summary>Périmètre : Global | Pole | Cellule | Service | Self.</summary>
    public string Scope { get; set; } = "Global";
    public bool IsAllowed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Étape du workflow de validation des fiches PRIME (configurable).</summary>
public class WorkflowStepConfigEntity
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Rôle valideur de l'étape (Référent technique | Superviseur | Chef de projet | RH).</summary>
    public string ApproverRole { get; set; } = "";
    /// <summary>Statut courant attendu avant validation (clé pour matcher l'état).</summary>
    public string FromStatus { get; set; } = "";
    /// <summary>Statut résultant après validation.</summary>
    public string ToStatus { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>SLA en heures avant alerte (0 = pas de SLA).</summary>
    public int SlaHours { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Configuration globale du workflow (singleton — 1 seule ligne).</summary>
public class WorkflowGlobalConfigEntity
{
    public Guid Id { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int GlobalSlaHours { get; set; } = 72;
    public bool AllowBulkApprove { get; set; } = true;
    public bool RequireRejectReason { get; set; } = true;
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Journal d'audit des actions structurelles et de validation.</summary>
public class AuditLogEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    /// <summary>Action : ValidationApproved | ValidationRejected | RbacChanged | WorkflowConfigChanged | OrgAssignmentChanged | …</summary>
    public string Action { get; set; } = "";
    /// <summary>Type d'entité affectée (EmployeePrimeServiceFiche | RbacPermission | …).</summary>
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    /// <summary>Détail libre (JSON sérialisé du diff / contexte).</summary>
    public string? DetailJson { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// Anomalie détectée sur une fiche PRIME (calcul incohérent, doublon, valeur hors bornes…).
/// Cycle de vie : Open → InReview → Resolved (ou Ignored).
/// </summary>
public class AnomalyEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>Type métier : ComputationMismatch | DuplicateFiche | OutOfRange | MissingApprover | StaleValidation | InvalidScope</summary>
    public string Type { get; set; } = "";
    /// <summary>Severity: Critical | High | Medium | Low.</summary>
    public string Severity { get; set; } = "Medium";
    /// <summary>Statut : Open | InReview | Resolved | Ignored.</summary>
    public string Status { get; set; } = "Open";
    public string Description { get; set; } = "";
    /// <summary>Référence cible (id fiche, indicateur, etc.).</summary>
    public string? TargetEntityType { get; set; }
    public string? TargetEntityId { get; set; }
    public string? Period { get; set; }
    public string? ServiceId { get; set; }
    public string? CelluleId { get; set; }
    public string? PoleId { get; set; }
    /// <summary>JSON métadonnées (snapshot des valeurs à la détection).</summary>
    public string? ContextJson { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
