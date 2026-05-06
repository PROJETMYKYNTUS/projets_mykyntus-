using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentationBackend.Services;

public interface IOriginalDocxTemplateRenderService
{
    byte[] Render(byte[] originalDocxBytes, IReadOnlyDictionary<string, string> values);
}

public sealed class OriginalDocxTemplateRenderService(
    ITemplatePlaceholderNormalizationService placeholderNormalization) : IOriginalDocxTemplateRenderService
{
    private static readonly Regex CurlyPlaceholderRegex = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex HumanPlaceholderRegex = new(@"\(([^\(\)\r\n]{1,120})\)", RegexOptions.Compiled);

    public byte[] Render(byte[] originalDocxBytes, IReadOnlyDictionary<string, string> values)
    {
        using var input = new MemoryStream(originalDocxBytes, writable: false);
        using var output = new MemoryStream();
        input.CopyTo(output);
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, true))
        {
            ProcessOpenXmlRoot(doc.MainDocumentPart?.Document?.Body, values);
            if (doc.MainDocumentPart is { } main)
            {
                foreach (var header in main.HeaderParts)
                    ProcessOpenXmlRoot(header.Header, values);
                foreach (var footer in main.FooterParts)
                    ProcessOpenXmlRoot(footer.Footer, values);
            }
            doc.Save();
        }

        return output.ToArray();
    }

    private void ProcessOpenXmlRoot(OpenXmlElement? root, IReadOnlyDictionary<string, string> values)
    {
        if (root is null)
            return;

        foreach (var paragraph in root.Descendants<Paragraph>())
            ReplaceInsideTextContainer(paragraph, values);
    }

    private string ReplaceText(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var rendered = CurlyPlaceholderRegex.Replace(text, match =>
        {
            var token = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
                return match.Value;

            var normalized = placeholderNormalization.NormalizeKey(token);
            var canonical = placeholderNormalization.ResolveCanonicalKey(normalized);
            return FirstNonEmpty(values, canonical, normalized, token) ?? string.Empty;
        });

        rendered = HumanPlaceholderRegex.Replace(rendered, match =>
        {
            var token = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
                return match.Value;

            var normalized = placeholderNormalization.NormalizeKey(token);
            var canonical = placeholderNormalization.ResolveCanonicalKey(normalized);
            return FirstNonEmpty(values, canonical, normalized, token) ?? string.Empty;
        });

        return rendered;
    }

    private void ReplaceInsideTextContainer(OpenXmlElement container, IReadOnlyDictionary<string, string> values)
    {
        var texts = container.Descendants<Text>().ToList();
        if (texts.Count == 0)
            return;

        var combined = string.Concat(texts.Select(t => t.Text ?? string.Empty));
        if (string.IsNullOrEmpty(combined))
            return;

        var replaced = ReplaceText(combined, values);
        if (string.Equals(combined, replaced, StringComparison.Ordinal))
            return;

        texts[0].Text = replaced;
        texts[0].Space = SpaceProcessingModeValues.Preserve;
        for (var i = 1; i < texts.Count; i++)
            texts[i].Text = string.Empty;
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var kv in values)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }
        }

        return null;
    }
}
