namespace PrimeBackend.Services;

/// <summary>
/// Règles RH pour l'écran « affectations par arbre » (pôle → cellule → service).
/// Implémentées dans <see cref="PrimeInMemoryStore"/> ; ce fichier sert de référence métier unique.
/// </summary>
/// <remarks>
/// <para><b>Cardinalité</b> : au plus <b>un</b> responsable actif par nœud pour Chef de projet (pôle),
/// Superviseur (cellule) et Référent technique (service). Une nouvelle affectation <b>remplace</b> la précédente sur ce nœud.</para>
/// <para><b>Pilote</b> : plusieurs pilotes par service possibles. Chaque pilote a un <c>serviceId</c> :
/// si non fourni à l'API, on prend le <b>premier service</b> de la cellule (ordre des listes en mémoire / seed).</para>
/// <para><b>Ancien responsable</b> : lorsqu'il est remplacé, il est rétrogradé en <c>Role = Pilote</c> sur le service
/// concerné (ou le premier service de la cellule / pôle selon le niveau), avec <c>parentId</c> aligné sur la
/// chaîne métier (référent technique du service si présent, sinon superviseur de la cellule, sinon chef de projet du pôle).</para>
/// <para><b>Rôles protégés</b> : RH, Admin, Audit ne peuvent pas recevoir ces affectations structurelles via l'API RH.</para>
/// </remarks>
internal static class OrgStructureRules
{
}
