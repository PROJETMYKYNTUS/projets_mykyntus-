namespace DocumentationBackend.Configuration;

/// <summary>En-tête / pied de page des documents RH (PDF unique source de vérité).</summary>
public sealed class DocumentBrandingOptions
{
    public const string SectionName = "DocumentBranding";

    public string CompanyName { get; set; } = "MyKyntus Maroc";

    /// <summary>Ville pour la mention « Fait à … » sur les PDF / exports (optionnel).</summary>
    public string DocumentCity { get; set; } = "";

    /// <summary>ICE (Identifiant Commun de l’Entreprise).</summary>
    public string Ice { get; set; } = "";

    /// <summary>Identifiant fiscal (IF).</summary>
    public string IdentifiantFiscal { get; set; } = "";

    public string Cnss { get; set; } = "";
    public string Rc { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";

    /// <summary>Chemin local ou URL HTTP(S) vers un logo (optionnel).</summary>
    public string LogoPath { get; set; } = "";
}
