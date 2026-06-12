namespace PrimeBackend.Services;

/// <summary>
/// Règles RH pour l'écran « affectations par arbre » (pôle → cellule → service).
/// </summary>
/// <remarks>
/// <para><b>Cardinalité</b> : plusieurs responsables actifs par nœud (Chef de projet, Superviseur, Référent technique).</para>
/// <para><b>Pilote</b> : plusieurs pilotes par service. ParentId = référent choisi à l'affectation (défaut : premier référent).</para>
/// <para><b>Rôles protégés</b> : RH, Admin, Audit ne peuvent pas recevoir ces affectations structurelles via l'API RH.</para>
/// <para><b>Noms</b> : unicité insensible à la casse — pôle (global), cellule (par pôle), service (par cellule).</para>
/// </remarks>
internal static class OrgStructureRules
{
    public const string DuplicatePoleNameMessage = "Un pôle porte déjà ce nom.";
    public const string DuplicateCelluleNameMessage = "Une cellule porte déjà ce nom pour ce pôle.";
    public const string DuplicateServiceNameMessage = "Un service porte déjà ce nom pour cette cellule.";

    public static string NormalizeOrgName(string? name) => (name ?? string.Empty).Trim();

    public static bool NamesEqual(string? a, string? b) =>
        string.Equals(NormalizeOrgName(a), NormalizeOrgName(b), StringComparison.OrdinalIgnoreCase);

    public static void EnsureUniquePoleName(IEnumerable<string> existingNames, string candidate)
    {
        var n = NormalizeOrgName(candidate);
        if (n.Length == 0)
            throw new ArgumentException("Le nom du pôle est requis.");
        if (existingNames.Any(e => NamesEqual(e, n)))
            throw new InvalidOperationException(DuplicatePoleNameMessage);
    }

    public static void EnsureUniqueCelluleName(IEnumerable<string> siblingNames, string candidate)
    {
        var n = NormalizeOrgName(candidate);
        if (n.Length == 0)
            throw new ArgumentException("Le nom est requis.");
        if (siblingNames.Any(e => NamesEqual(e, n)))
            throw new InvalidOperationException(DuplicateCelluleNameMessage);
    }

    public static void EnsureUniqueServiceName(IEnumerable<string> siblingNames, string candidate)
    {
        var n = NormalizeOrgName(candidate);
        if (n.Length == 0)
            throw new ArgumentException("Le nom est requis.");
        if (siblingNames.Any(e => NamesEqual(e, n)))
            throw new InvalidOperationException(DuplicateServiceNameMessage);
    }
}
