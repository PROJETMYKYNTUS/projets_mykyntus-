using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Service métier qui encapsule les règles de transition du workflow de validation
/// d'une <see cref="EmployeePrimeServiceFicheEntity"/> (Phase 1.1).
///
/// Workflow fiche employé (rôles opérationnels) :
///     Pending → Référent technique Approved → Superviseur Approved → Chef de projet Approved (terminal)
///     RH / Manager / Comptabilité : fichier synthèse globale, pas ce flux.
/// </summary>
public static class PrimeValidationWorkflowService
{
    /// <summary>Hors file de validation : partie commune ou cellule incomplète.</summary>
    public const string AwaitingData = "AwaitingData";

    public const string Pending = "Pending";
    /// <summary>Conservé pour compatibilité données / API ; n’est plus une étape du flux ordonné.</summary>
    public const string ReferentTechniqueApproved = "Référent technique Approved";
    public const string SuperviseurApproved = "Superviseur Approved";
    public const string ChefDeProjetApproved = "Chef de projet Approved";
    public const string RhApproved = "RH Approved";
    public const string Rejected = "Rejected";
    /// <summary>Fiche issue d'un import « historique » (hors flux de validation actif).</summary>
    public const string HistoricalImport = "Historical Import";

    private static readonly string[] OrderedFlow =
    [
        Pending,
        ReferentTechniqueApproved,
        SuperviseurApproved,
        ChefDeProjetApproved,
    ];

    /// <summary>Liste ordonnée des statuts du flux (sans Rejected).</summary>
    public static IReadOnlyList<string> Flow => OrderedFlow;

    /// <summary>Tous les statuts valides (flux + Rejected + ancien statut RT pour filtres / données historiques).</summary>
    public static IReadOnlyList<string> AllStatuses =>
        [AwaitingData, .. OrderedFlow, Rejected, ReferentTechniqueApproved, HistoricalImport];

    /// <summary>Vrai si la fiche provient d'un import historique (archive figée hors flux).</summary>
    public static bool IsHistoricalImport(string? status) =>
        string.Equals(status?.Trim(), HistoricalImport, StringComparison.Ordinal);

    /// <summary>Rôle attendu pour faire la prochaine validation depuis l'état courant.</summary>
    public static string? RequiredApproverRole(string currentStatus) => currentStatus switch
    {
        Pending => PrimeFicheValidationRoles.ReferentTechnique,
        ReferentTechniqueApproved => PrimeFicheValidationRoles.Superviseur,
        SuperviseurApproved => PrimeFicheValidationRoles.ChefDeProjet,
        _ => null,
    };

    /// <summary>Vrai si <paramref name="role"/> peut faire avancer la fiche depuis <paramref name="currentStatus"/>.</summary>
    public static bool CanApprove(string currentStatus, string role)
    {
        var required = RequiredApproverRole(currentStatus);
        if (required is null) return false;
        return string.Equals(required, role, StringComparison.Ordinal);
    }

    /// <summary>Vrai si <paramref name="role"/> peut rejeter la fiche depuis <paramref name="currentStatus"/>.</summary>
    public static bool CanReject(string currentStatus, string role)
    {
        if (currentStatus is ChefDeProjetApproved or RhApproved or Rejected or HistoricalImport) return false;
        var required = RequiredApproverRole(currentStatus);
        return required is not null && string.Equals(required, role, StringComparison.Ordinal);
    }

    /// <summary>Statut suivant dans le flux (ou null si déjà au bout / Rejected).</summary>
    public static string? NextStatus(string currentStatus)
    {
        var idx = Array.IndexOf(OrderedFlow, currentStatus);
        if (idx < 0 || idx == OrderedFlow.Length - 1) return null;
        return OrderedFlow[idx + 1];
    }

    /// <summary>Hors circuit de validation (saisie / attente de soumission automatique en Pending).</summary>
    public static bool IsPreWorkflowStatus(string? status)
    {
        var s = status?.Trim() ?? "";
        return string.Equals(s, AwaitingData, StringComparison.Ordinal) ||
               string.Equals(s, "NotStarted", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrEmpty(s);
    }

    public static bool IsValidStatus(string status) =>
        AllStatuses.Contains(status, StringComparer.Ordinal) ||
        string.Equals(status, AwaitingData, StringComparison.Ordinal);

    /// <summary>Applique l'approbation et met à jour les champs de validation de la fiche.</summary>
    public static void ApproveOrThrow(EmployeePrimeServiceFicheEntity fiche, string approverUserId, string approverRole, DateTimeOffset now)
    {
        if (!CanApprove(fiche.ValidationStatus, approverRole))
            throw new InvalidOperationException($"Le rôle « {approverRole} » ne peut pas valider depuis l'état « {fiche.ValidationStatus} ».");
        var next = NextStatus(fiche.ValidationStatus)
            ?? throw new InvalidOperationException("Pas de statut suivant disponible.");
        ApplyApproval(fiche, next, approverUserId, now);
    }

    /// <summary>Applique une transition d’approbation connue (ex. étapes chargées depuis la base).</summary>
    public static void ApplyApproval(EmployeePrimeServiceFicheEntity fiche, string nextStatus, string approverUserId, DateTimeOffset now)
    {
        fiche.ValidationStatus = nextStatus;
        fiche.LastApproverUserId = approverUserId;
        fiche.LastApprovedAt = now;
        fiche.RejectedByUserId = null;
        fiche.RejectedAt = null;
        fiche.RejectionReason = null;
        fiche.UpdatedAt = now;
    }

    /// <summary>Applique le rejet et met à jour les champs de validation de la fiche.</summary>
    public static void RejectOrThrow(EmployeePrimeServiceFicheEntity fiche, string rejecterUserId, string rejecterRole, string reason, DateTimeOffset now)
    {
        if (!CanReject(fiche.ValidationStatus, rejecterRole))
            throw new InvalidOperationException($"Le rôle « {rejecterRole} » ne peut pas rejeter depuis l'état « {fiche.ValidationStatus} ».");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Un motif de rejet est obligatoire.", nameof(reason));
        ApplyReject(fiche, rejecterUserId, reason.Trim(), now);
    }

    public static void ApplyReject(EmployeePrimeServiceFicheEntity fiche, string rejecterUserId, string reason, DateTimeOffset now)
    {
        fiche.ValidationStatus = Rejected;
        fiche.RejectedByUserId = rejecterUserId;
        fiche.RejectedAt = now;
        fiche.RejectionReason = reason;
        fiche.UpdatedAt = now;
    }
}
