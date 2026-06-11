using System.Text.Json;
using DocumentationBackend.Api;
using DocumentationBackend.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DocumentationBackend.Services;

public enum AiDirectFillValidationPolicy
{
    /// <summary>Placeholders détectés par motifs étendus + données critiques dans le texte.</summary>
    Full,

    /// <summary>UI « Génération de documents » : ERROR_MISSING_DATA + résidu (X), (), etc. (sans contrôle « données critiques »).</summary>
    GenerateDocumentAiUi,
}

public sealed record AiDirectFillOutcome(bool Success, string? Document, int ErrorStatusCode, object ErrorBody);

/// <summary>Orchestre l’appel OpenAI et la validation post-réponse (aucun remplacement local).</summary>
public sealed class AiDirectDocumentFillOrchestrator(IAiTemplateContentGenerator aiTemplateGenerator)
{
    public const int MaxTemplateLength = 100_000;

    public async Task<AiDirectFillOutcome> FillAsync(
        AiDirectDocumentFillRequest body,
        AiDirectFillValidationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Template))
            return new AiDirectFillOutcome(false, null, StatusCodes.Status400BadRequest, new { message = "template est obligatoire." });
        if (body.Template.Length > MaxTemplateLength)
            return new AiDirectFillOutcome(false, null, StatusCodes.Status400BadRequest, new { message = "template trop volumineux." });

        var dbEl = body.DbData ?? JsonDocument.Parse("{}").RootElement;
        var formEl = body.FormData ?? JsonDocument.Parse("{}").RootElement;

        if (!await aiTemplateGenerator.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AiDirectFillOutcome(false, null, StatusCodes.Status503ServiceUnavailable,
                new { message = "Clé API IA non configurée." });
        }

        AiDirectRawFillResult raw;
        try
        {
            raw = await aiTemplateGenerator.FillRhTemplateDirectAsync(body.Template, dbEl, formEl, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return new AiDirectFillOutcome(false, null, StatusCodes.Status400BadRequest, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return new AiDirectFillOutcome(false, null, StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        var reasons = new List<string>();
        if (raw.ModelReportedMissingData)
        {
            reasons.Add(
                policy == AiDirectFillValidationPolicy.Full
                    ? "Le modèle IA a signalé des données insuffisantes (ERROR_MISSING_DATA)."
                    : "ERROR_MISSING_DATA");
        }

        if (!raw.ModelReportedMissingData)
        {
            if (policy == AiDirectFillValidationPolicy.Full)
            {
                var placeholders = AiDirectFilledDocumentValidator.FindRemainingPlaceholders(raw.RawText);
                if (placeholders.Count > 0)
                    reasons.Add("Placeholders non résolus : " + string.Join(", ", placeholders));

                var merged = AiDirectFilledDocumentValidator.MergeFlattenedData(dbEl, formEl);
                foreach (var msg in AiDirectFilledDocumentValidator.FindCriticalDataNotReflectedInOutput(merged, raw.RawText))
                    reasons.Add(msg);
            }
            else
            {
                var simple = AiDirectFilledDocumentValidator.FindUserListedResidualPlaceholders(raw.RawText);
                if (simple.Count > 0)
                    reasons.Add("Marqueurs non résolus : " + string.Join(", ", simple));
            }
        }

        if (reasons.Count > 0)
        {
            string? friendly = null;
            if (policy == AiDirectFillValidationPolicy.GenerateDocumentAiUi)
            {
                friendly = raw.ModelReportedMissingData || reasons.Any(r => r.Contains("ERROR_MISSING_DATA", StringComparison.Ordinal))
                    ? "Données manquantes pour générer le document."
                    : "Le document généré contient encore des zones à compléter.";
            }

            var payload = new AiDirectDocumentFillResponse("rejected", null, reasons, raw.ModelReportedMissingData, friendly);
            return new AiDirectFillOutcome(false, null, StatusCodes.Status422UnprocessableEntity, payload);
        }

        if (string.IsNullOrWhiteSpace(raw.RawText))
        {
            var friendly = policy == AiDirectFillValidationPolicy.GenerateDocumentAiUi
                ? "Données manquantes pour générer le document."
                : null;
            var payload = new AiDirectDocumentFillResponse(
                "rejected",
                null,
                new[] { "Document vide après génération." },
                raw.ModelReportedMissingData,
                friendly);
            return new AiDirectFillOutcome(false, null, StatusCodes.Status422UnprocessableEntity, payload);
        }

        return new AiDirectFillOutcome(true, raw.RawText, StatusCodes.Status200OK, new object());
    }
}
