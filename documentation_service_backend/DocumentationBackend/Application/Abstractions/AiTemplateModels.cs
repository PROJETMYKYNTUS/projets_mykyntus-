namespace DocumentationBackend.Application.Abstractions;

/// <summary>Variable détectée et typée par l’API IA (plan B enrichissement).</summary>
public sealed record AiDetectedVariable(
    string Name,
    string Type,
    bool IsRequired,
    string? ValidationRule,
    string? Description);

/// <summary>Suggestion de code, nom et description pour un template importé.</summary>
public sealed record AiTemplateMetaSuggestion(
    string SuggestedCode,
    string SuggestedName,
    string SuggestedDescription);
