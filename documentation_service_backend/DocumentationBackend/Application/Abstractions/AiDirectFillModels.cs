namespace DocumentationBackend.Application.Abstractions;

/// <summary>Résultat brut de l’API avant validation serveur (placeholders / données critiques).</summary>
public sealed record AiDirectRawFillResult(
    string RawText,
    bool ModelReportedMissingData);
