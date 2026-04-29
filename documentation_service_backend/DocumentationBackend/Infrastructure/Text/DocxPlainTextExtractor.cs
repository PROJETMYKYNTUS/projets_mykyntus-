using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentationBackend.Infrastructure.Text;

/// <summary>
/// Concatène tous les nœuds <c>w:t</c> du corps, en-têtes et pieds (ordre logique du document).
/// Plus fiable que le seul <c>InnerText</c> du corps si Word fragmente les placeholders sur plusieurs runs.
/// </summary>
internal static class DocxPlainTextExtractor
{
    private static readonly Regex BookmarkNamePattern = new(@"^[a-zA-Z0-9_]{2,40}$", RegexOptions.Compiled);

    private static readonly Regex LegacyDateInTextRegex = new(@"XX/XX/20\d{2}", RegexOptions.Compiled);

    /// <summary>Supprime espaces insécables / caractères de largeur nulle souvent insérés par Word.</summary>
    internal static string NormalizeForPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var s = Regex.Replace(text, @"[\u200B-\u200D\uFEFF]", "");
        s = s.Replace("\u00A0", "", StringComparison.Ordinal).Replace("\u2060", "", StringComparison.Ordinal);
        return s;
    }

    /// <summary>
    /// Extrait le texte du DOCX, ajoute en fin les signets utilisateur valides sous forme de <c>{{name}}</c> pour la détection des variables.
    /// </summary>
    internal static string Extract(Stream stream)
    {
        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, false);
        var sb = new StringBuilder();
        AppendWordTextNodes(doc, sb);

        var bookmarkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectBookmarkStartNames(doc, bookmarkNames);
        foreach (var name in bookmarkNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsUserFacingBookmarkName(name))
                continue;
            sb.Append("{{").Append(name).Append("}}");
        }

        return NormalizeForPlaceholders(sb.ToString());
    }

    /// <summary>
    /// Détecte les marqueurs hérités Word dans le texte brut du document : <c>(X)</c>, <c>(XXXX)</c>, masque date <c>XX/XX/20xx</c>.
    /// </summary>
    /// <param name="stream">Flux positionné au début ; sera relu depuis le début.</param>
    /// <returns>Liste distincte ordonnée des types : <c>LEGACY_X</c>, <c>LEGACY_XXXX</c>, <c>LEGACY_DATE</c>.</returns>
    internal static IReadOnlyList<string> ExtractLegacyMarkers(Stream stream)
    {
        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, false);
        var sb = new StringBuilder();
        AppendWordTextNodes(doc, sb);
        var normalized = NormalizeForPlaceholders(sb.ToString());

        var markers = new List<string>(3);
        if (normalized.Contains("(X)", StringComparison.Ordinal))
            markers.Add("LEGACY_X");
        if (normalized.Contains("(XXXX)", StringComparison.Ordinal))
            markers.Add("LEGACY_XXXX");
        if (LegacyDateInTextRegex.IsMatch(normalized))
            markers.Add("LEGACY_DATE");

        markers.Sort(StringComparer.Ordinal);
        return markers;
    }

    private static void AppendWordTextNodes(WordprocessingDocument doc, StringBuilder sb)
    {
        var main = doc.MainDocumentPart;
        if (main?.Document?.Body is { } body)
        {
            foreach (var t in body.Descendants<W.Text>())
                sb.Append(t.Text ?? "");
        }

        if (main is null)
            return;

        foreach (var part in main.HeaderParts)
        {
            if (part.Header is null)
                continue;
            foreach (var t in part.Header.Descendants<W.Text>())
                sb.Append(t.Text ?? "");
        }

        foreach (var part in main.FooterParts)
        {
            if (part.Footer is null)
                continue;
            foreach (var t in part.Footer.Descendants<W.Text>())
                sb.Append(t.Text ?? "");
        }
    }

    private static void CollectBookmarkStartNames(WordprocessingDocument doc, HashSet<string> target)
    {
        var main = doc.MainDocumentPart;
        if (main?.Document?.Body is { } body)
        {
            foreach (var bm in body.Descendants<W.BookmarkStart>())
                AddBookmarkName(bm.Name?.Value, target);
        }

        if (main is null)
            return;

        foreach (var part in main.HeaderParts)
        {
            if (part.Header is null)
                continue;
            foreach (var bm in part.Header.Descendants<W.BookmarkStart>())
                AddBookmarkName(bm.Name?.Value, target);
        }

        foreach (var part in main.FooterParts)
        {
            if (part.Footer is null)
                continue;
            foreach (var bm in part.Footer.Descendants<W.BookmarkStart>())
                AddBookmarkName(bm.Name?.Value, target);
        }
    }

    private static void AddBookmarkName(string? name, HashSet<string> target)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var trimmed = name.Trim();
        if (!BookmarkNamePattern.IsMatch(trimmed))
            return;
        if (!IsUserFacingBookmarkName(trimmed))
            return;
        target.Add(trimmed);
    }

    /// <summary>Exclut les signets internes Word.</summary>
    private static bool IsUserFacingBookmarkName(string name) =>
        !string.Equals(name, "_GoBack", StringComparison.Ordinal)
        && !name.StartsWith("OLE_LINK", StringComparison.Ordinal);
}
