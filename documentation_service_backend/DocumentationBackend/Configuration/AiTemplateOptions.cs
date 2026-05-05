namespace DocumentationBackend.Configuration;

/// <summary>Options OpenAI / compatible pour templates (<c>DocumentTemplates:Ai</c>).</summary>
public sealed class AiTemplateOptions
{
    public const string SectionPath = "DocumentTemplates:Ai";

    /// <summary>Point d’accès OpenAI-compatible (ex. https://api.openai.com/v1).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Ne jamais committer : User Secrets, variable d’environnement ou coffre.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Modèle rapide et économique par défaut.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 4096;

    /// <summary>Plafond de sortie pour le remplissage IA direct (documents longs).</summary>
    public int DirectFillMaxTokens { get; set; } = 8192;

    /// <summary>Inférence héritée : liste simple de noms (legacy).</summary>
    public bool InferVariablesOnUpload { get; set; } = true;

    public int MaxCharsForUploadInference { get; set; } = 12000;

    public bool EnableVariableDetection { get; set; } = true;

    public bool EnableMetaSuggestion { get; set; } = true;

    public int MaxBodyCharsForDetection { get; set; } = 6000;

    public int MaxBodyCharsForMeta { get; set; } = 3000;

    /// <summary>
    /// Après fusion annuaire / saisie, appelle l’IA pour corriger civilité, CIN factice, confusion poste / rôle applicatif / service.
    /// </summary>
    public bool EnableVariableRefinementOnGenerate { get; set; } = false;

    /// <summary>Taille max (caractères) du JSON envoyé pour l’affinage des variables.</summary>
    public int MaxCharsVariableRefinementPayload { get; set; } = 4500;
}
