using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Service métier qui encapsule les règles de transition du workflow de validation
/// d'une <see cref="EmployeePrimeServiceFicheEntity"/> (Phase 1.1).
///
/// Workflow fiche employé (sans étape bloquante « Référent technique » — lecture seule côté métier) :
///     Pending → Superviseur Approved (niveau 1) → Chef de projet Approved (niveau 2) → RH Approved
///     (toute étape pré-RH peut tomber en Rejected)
/// </summary>
public static class PrimeValidationWorkflowService
{
    public const string Pending = "Pending";
    /// <summary>Conservé pour compatibilité données / API ; n’est plus une étape du flux ordonné.</summary>
    public const string ReferentTechniqueApproved = "Référent technique Approved";
    public const string SuperviseurApproved = "Superviseur Approved";
    public const string ChefDeProjetApproved = "Chef de projet Approved";
    public const string RhApproved = "RH Approved";
    public const string Rejected = "Rejected";

    private static readonly string[] OrderedFlow =
    [
        Pending,
        SuperviseurApproved,
        ChefDeProjetApproved,
        RhApproved,
    ];

    /// <summary>Liste ordonnée des statuts du flux (sans Rejected).</summary>
    public static IReadOnlyList<string> Flow => OrderedFlow;

    /// <summary>Tous les statuts valides (flux + Rejected + ancien statut RT pour filtres / données historiques).</summary>
    public static IReadOnlyList<string> AllStatuses => [.. OrderedFlow, Rejected, ReferentTechniqueApproved];

    /// <summary>Rôle attendu pour faire la prochaine validation depuis l'état courant.</summary>
    public static string? RequiredApproverRole(string currentStatus) => currentStatus switch
    {
        Pending => "Superviseur",
        SuperviseurApproved => "Chef de projet",
        ChefDeProjetApproved => "RH",
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
        // Tant que la fiche n'est pas RH Approved ni déjà Rejected, le valideur courant peut rejeter.
        if (currentStatus is RhApproved or Rejected) return false;
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

    public static bool IsValidStatus(string status) =>
        AllStatuses.Contains(status, StringComparer.Ordinal);

    /// <summary>Applique l'approbation et met à jour les champs de validation de la fiche.</summary>
    public static void ApproveOrThrow(EmployeePrimeServiceFicheEntity fiche, string approverUserId, string approverRole, DateTimeOffset now)
    {
        if (!CanApprove(fiche.ValidationStatus, approverRole))
            throw new InvalidOperationException($"Le rôle « {approverRole} » ne peut pas valider depuis l'état « {fiche.ValidationStatus} ».");
        var next = NextStatus(fiche.ValidationStatus)
            ?? throw new InvalidOperationException("Pas de statut suivant disponible.");
        fiche.ValidationStatus = next;
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
        fiche.ValidationStatus = Rejected;
        fiche.RejectedByUserId = rejecterUserId;
        fiche.RejectedAt = now;
        fiche.RejectionReason = reason.Trim();
        fiche.UpdatedAt = now;
    }
}
