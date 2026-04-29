using NpgsqlTypes;

namespace DocumentationBackend.Data;

public enum DocumentRequestStatus
{
    [PgName("pending")] Pending,
    [PgName("approved")] Approved,
    [PgName("rejected")] Rejected,
    [PgName("generated")] Generated,
    [PgName("cancelled")] Cancelled,
}

public enum GeneratedDocumentStatus
{
    [PgName("pending")] Pending,
    /// <summary>Brouillon : texte généré ; le PDF est produit après validation RH (statut generated).</summary>
    [PgName("draft_pending_rh")] DraftPendingRhReview,
    [PgName("generated")] Generated,
    [PgName("approved")] Approved,
    [PgName("rejected")] Rejected,
    [PgName("archived")] Archived,
    [PgName("expired")] Expired,
}

public enum WorkflowNotificationKey
{
    [PgName("email")] Email,
    [PgName("none")] None,
}

public enum WorkflowActionKey
{
    [PgName("validate")] Validate,
    [PgName("reject")] Reject,
    [PgName("approve")] Approve,
    [PgName("archive")] Archive,
}

public enum AppRole
{
    [PgName("pilote")] Pilote,
    [PgName("coach")] Coach,
    [PgName("manager")] Manager,
    [PgName("rp")] Rp,
    [PgName("rh")] Rh,
    [PgName("admin")] Admin,
    [PgName("audit")] Audit,
}

public enum StorageType
{
    [PgName("local")] Local,
    [PgName("cloud")] Cloud,
}

/// <summary>Modèle dynamique (JSON + variables) ou fichier statique (PDF/DOCX prêt).</summary>
public enum DocumentTemplateKind
{
    [PgName("dynamic")] Dynamic,
    [PgName("static")] Static,
}
