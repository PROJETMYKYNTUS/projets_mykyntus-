namespace PrimeBackend.Services;

/// <summary>
/// Règles RH pour l’écran « affectations par arbre » (département → pôle → cellule).
/// Implémentées dans <see cref="PrimeInMemoryStore"/> ; ce fichier sert de référence métier unique.
/// </summary>
/// <remarks>
/// <para><b>Cardinalité</b> : au plus <b>un</b> responsable actif par nœud pour Manager (département),
/// Superviseur (pôle) et Coach (cellule). Une nouvelle affectation <b>remplace</b> la précédente sur ce nœud.</para>
/// <para><b>Pilote</b> : plusieurs pilotes par cellule possibles. Chaque pilote a un <c>teamId</c> :
/// si non fourni à l’API, on prend la <b>première équipe</b> de la cellule (ordre des listes en mémoire / seed).</para>
/// <para><b>Ancien responsable</b> : lorsqu’il est remplacé, il est rétrogradé en <c>Role = Pilote</c> sur la cellule
/// concernée (ou la première cellule du pôle / département selon le niveau), avec <c>parentId</c> aligné sur la
/// chaîne métier (coach de la cellule si présent, sinon superviseur du pôle, sinon manager du département).</para>
/// <para><b>Rôles protégés</b> : RH, Admin, Audit ne peuvent pas recevoir ces affectations structurelles via l’API RH.</para>
/// </remarks>
internal static class OrgStructureRules
{
}
