namespace DocumentationBackend.Configuration;

/// <summary>Workflow génération : brouillon éditable RH avant PDF final.</summary>
public sealed class DocumentWorkflowOptions
{
    public const string SectionName = "DocumentWorkflow";

    /// <summary>Si vrai, les modèles dynamiques produisent un brouillon (pas de PDF) jusqu’à finalisation RH.</summary>
    public bool RequireRhEditorReview { get; set; } = true;

    /// <summary>Dans le texte éditable RH, remplace les variables obligatoires vides par ce marqueur (aperçu PDF inclus).</summary>
    public bool MarkMissingFieldsInRhDraft { get; set; } = true;

    /// <summary>Texte affiché pour une variable obligatoire encore vide (traçabilité visuelle).</summary>
    public string MissingFieldPlaceholder { get; set; } = "________";
}
