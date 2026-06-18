using System.Text.Json;

namespace DocumentationBackend.Application.Abstractions;

/// <summary>Génération de contenu structuré (JSON) pour template métier via API externe.</summary>
public interface IAiTemplateContentGenerator
{
    /// <summary>True si une clé est renseignée dans appsettings (ne reflète pas forcément la clé DB admin).</summary>
    bool IsConfigured { get; }

    /// <summary>True si une clé utilisable est disponible (base <c>ai_api_keys</c> active ou appsettings).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Retourne un JSON string (sections, titres) prêt pour structuredContent.</summary>
    Task<string> GenerateStructuredTemplateJsonAsync(string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// À partir du texte brut extrait d’un document RH, propose des noms de variables (snake_case)
    /// pour les informations qui changent d’un dossier à l’autre (hors texte juridique fixe).
    /// </summary>
    Task<IReadOnlyList<string>> InferVariableNamesFromUploadTextAsync(
        string documentPlainText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demande à l’IA de détecter et typer toutes les variables du texte (placeholders, marqueurs legacy).
    /// </summary>
    Task<IReadOnlyList<AiDetectedVariable>> DetectVariablesFromTextAsync(
        string bodyText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggestion de code canonique, nom d’affichage et description courte à partir du corps du document.
    /// </summary>
    Task<AiTemplateMetaSuggestion> SuggestTemplateMetaAsync(
        string bodyText,
        string? userHint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Affinage des valeurs fusionnées avant rendu PDF : civilité (Monsieur/Madame), CIN hors placeholder,
    /// intitulé de poste métier vs rôle applicatif (pilote, coach…) vs service / département.
    /// </summary>
    /// <returns>Clés à mettre à jour uniquement (insensible à la casse) ; dictionnaire vide si désactivé ou en erreur.</returns>
    Task<IReadOnlyDictionary<string, string>> RefineMergedVariablesForDocumentAsync(
        IReadOnlyDictionary<string, string> mergedVariables,
        IReadOnlyList<string> templateVariableNames,
        string? documentTitle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplissage « IA directe » : aucun moteur local de substitution — le modèle produit le document final.
    /// </summary>
    Task<AiDirectRawFillResult> FillRhTemplateDirectAsync(
        string template,
        JsonElement dbData,
        JsonElement formData,
        CancellationToken cancellationToken = default);
}
