using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentationBackend.Services;

/// <summary>Extrait titre, bloc principal et signature depuis le texte structuré (marqueurs [EN_TETE], [CORPS], [SIGNATURE]).</summary>
public static class StructuredDocumentExportParser
{
    public sealed record Parts(string Title, string MainText, string SignatureText);

    /// <summary>Lettres majuscules début de phrase en français (hors ligatures rares).</summary>
    private static readonly Regex MissingSpaceAfterPeriod =
        new(@"\.(?=[A-ZÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝŸ])", RegexOptions.Compiled);

    private static readonly Regex[] ClauseParagraphBreaks =
    {
        new(@"(\.)\s+(Par la présente)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"(\.)\s+(Nous nous engageons)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"(\.)\s+(Veuillez agréer)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
    };

    private static readonly string[] LegalFooterTokens = ["ICE", "IF", "CNSS", "RC", "E-mail", "Email", "Tél", "Tel", "Téléphone"];

    private static readonly Regex SignatureTailFromPlaceDateRegex = new(
        @"\bFait\s+à\b[\s\S]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SignatureTailFromManagerRegex = new(
        @"\bLa\s+G[ée]rante\b[\s\S]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Parts Parse(string rendered, string fallbackTitle)
    {
        var norm = (rendered ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        var titleFallback = string.IsNullOrWhiteSpace(fallbackTitle) ? "Document" : fallbackTitle.Trim();

        var iEn = norm.IndexOf("[EN_TETE]", StringComparison.OrdinalIgnoreCase);
        var iCorps = norm.IndexOf("[CORPS]", StringComparison.OrdinalIgnoreCase);
        var iSig = norm.IndexOf("[SIGNATURE]", StringComparison.OrdinalIgnoreCase);

        var afterEn = iEn >= 0 ? iEn + "[EN_TETE]".Length : -1;
        var afterCorps = iCorps >= 0 ? iCorps + "[CORPS]".Length : -1;

        string? header = null;
        if (iEn >= 0)
        {
            var headerEnd = iCorps >= 0 ? iCorps : (iSig >= 0 ? iSig : norm.Length);
            if (headerEnd > afterEn)
                header = norm[afterEn..headerEnd].Trim();
        }

        string bodyCorps = "";
        if (iCorps >= 0)
        {
            var corpsEnd = iSig >= 0 ? iSig : norm.Length;
            if (corpsEnd > afterCorps)
                bodyCorps = norm[afterCorps..corpsEnd].Trim();
        }
        else if (iEn < 0 && iSig < 0)
        {
            bodyCorps = norm.Trim();
        }
        else if (iEn >= 0 && iCorps < 0)
        {
            var start = afterEn;
            var end = iSig >= 0 ? iSig : norm.Length;
            if (end > start)
                bodyCorps = norm[start..end].Trim();
        }

        var signature = "";
        if (iSig >= 0)
        {
            var sigStart = iSig + "[SIGNATURE]".Length;
            if (sigStart < norm.Length)
                signature = norm[sigStart..].Trim();
        }

        signature = NormalizeSignatureText(signature);

        var title = titleFallback;
        var mainParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(header))
        {
            var hl = header.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (hl.Length > 0)
                title = hl[0].Trim();
            if (hl.Length > 1)
                mainParts.Add(string.Join("\n", hl.Skip(1)));
        }

        if (!string.IsNullOrWhiteSpace(bodyCorps))
            mainParts.Add(bodyCorps);

        var main = mainParts.Count == 0 ? " " : string.Join("\n\n", mainParts);
        main = string.IsNullOrWhiteSpace(main) ? " " : NormalizeExportMainText(main, title);

        return new Parts(title, StripLegalFooterNoise(main), signature);
    }

    /// <summary>
    /// Nettoie le corps pour l’export : évite la répétition du titre déjà affiché en page, restaure les espaces après points,
    /// insère des sauts de paragraphe avant les clauses juridiques courantes.
    /// </summary>
    public static string NormalizeExportMainText(string mainText, string documentTitle)
    {
        if (string.IsNullOrWhiteSpace(mainText))
            return " ";
        var t = mainText.Trim();
        t = RemoveLeadingRedundantTitle(t, documentTitle);
        t = MissingSpaceAfterPeriod.Replace(t, ". ");
        foreach (var rx in ClauseParagraphBreaks)
            t = rx.Replace(t, "$1\n\n$2");
        t = StripEmbeddedSignatureTail(t);
        return string.IsNullOrWhiteSpace(t) ? " " : t;
    }

    private static string StripLegalFooterNoise(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return " ";

        var cleaned = text;
        foreach (var token in LegalFooterTokens)
        {
            var idx = cleaned.IndexOf(token + " :", StringComparison.OrdinalIgnoreCase);
            if (idx <= 0)
                idx = cleaned.IndexOf(token + ":", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                cleaned = cleaned[..idx].TrimEnd();
                break;
            }
        }

        return string.IsNullOrWhiteSpace(cleaned) ? " " : cleaned;
    }

    private static string NormalizeSignatureText(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return "La Gérante\nMme RABHA HERRAS\n(Signature et cachet)";

        var s = StripLegalFooterNoise(signature).Trim();
        if (!s.Contains("Signature", StringComparison.OrdinalIgnoreCase))
            s += "\n(Signature et cachet)";
        return s;
    }

    /// <summary>Supprime le titre en doublon collé ou espacé en tête du corps (ex. titre PDF + même libellé dans le flux Word).</summary>
    private static string RemoveLeadingRedundantTitle(string main, string title)
    {
        if (string.IsNullOrWhiteSpace(main) || string.IsNullOrWhiteSpace(title))
            return main;
        var m = main.TrimStart();
        var t = CollapseWs(title.Trim());
        if (t.Length == 0)
            return main;
        var prefixOffset = 0;
        while (prefixOffset < m.Length)
        {
            var ch = m[prefixOffset];
            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '.' or ':' or ')' or '(' or '-' or '–' or '—')
            {
                prefixOffset++;
                continue;
            }

            break;
        }

        var candidate = prefixOffset > 0 && prefixOffset < m.Length ? m[prefixOffset..] : m;
        var headLen = MatchTitlePrefixLength(candidate, t);
        if (headLen <= 0)
            return main;
        var absoluteHead = prefixOffset + headLen;
        var after = m[absoluteHead..].TrimStart();
        after = after.TrimStart(':', '—', '-', '·', '.', ',', ';');
        return string.IsNullOrWhiteSpace(after) ? main : after;
    }

    private static string StripEmbeddedSignatureTail(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return " ";

        var cut = SignatureTailFromPlaceDateRegex.Replace(text, "").TrimEnd();
        cut = SignatureTailFromManagerRegex.Replace(cut, "").TrimEnd();
        return string.IsNullOrWhiteSpace(cut) ? " " : cut;
    }

    private static int MatchTitlePrefixLength(string mainCollapsedCandidate, string titleCollapsed)
    {
        var i = 0;
        var j = 0;
        var main = mainCollapsedCandidate.AsSpan();
        while (i < main.Length && j < titleCollapsed.Length)
        {
            if (char.ToUpperInvariant(main[i]) == char.ToUpperInvariant(titleCollapsed[j]))
            {
                i++;
                j++;
                continue;
            }

            if (char.IsWhiteSpace(main[i]))
            {
                i++;
                continue;
            }

            if (char.IsWhiteSpace(titleCollapsed[j]))
            {
                j++;
                continue;
            }

            return -1;
        }

        while (i < main.Length && char.IsWhiteSpace(main[i]))
            i++;
        if (j < titleCollapsed.Length)
            return -1;
        return i;
    }

    private static string CollapseWs(string s)
    {
        var sb = new StringBuilder(s.Length);
        var prevSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                prevSpace = true;
                continue;
            }

            if (prevSpace && sb.Length > 0)
                sb.Append(' ');
            prevSpace = false;
            sb.Append(ch);
        }

        return sb.ToString();
    }
}
