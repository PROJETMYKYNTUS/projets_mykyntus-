using System.Globalization;
using System.Net;
using System.Text;

namespace DocumentationBackend.Services;

public static class StructuredDocumentHtmlTxtExporter
{
    public static byte[] BuildHtmlUtf8(string pageTitle, StructuredDocumentExportParser.Parts parts, string watermark)
    {
        var title = WebUtility.HtmlEncode(parts.Title);
        var main = EncodeAsHtmlParagraphs(parts.MainText);
        var sig = WebUtility.HtmlEncode(parts.SignatureText).Replace("\n", "<br/>", StringComparison.Ordinal);
        var wm = WebUtility.HtmlEncode(watermark);
        var pt = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(pageTitle) ? parts.Title : pageTitle);

        var sb = new StringBuilder(1024);
        sb.Append("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\"/>");
        sb.Append(CultureInfo.InvariantCulture, $"<title>{pt}</title>");
        // Typographie alignée usage international (Arial/Helvetica, 11pt corps, interligne 1,15, veuves/orphelines).
        sb.Append("<style>");
        sb.Append("@page{size:A4;margin:2.54cm;}");
        sb.Append("body{font-family:Helvetica,Arial,sans-serif;font-size:11pt;line-height:1.15;color:#000000;");
        sb.Append("text-align:justify;max-width:720px;margin:0 auto;padding:0;}");
        sb.Append("h1,.title{font-size:16pt;font-weight:bold;margin-top:24pt;margin-bottom:12pt;text-align:center;");
        sb.Append("page-break-after:avoid;}");
        sb.Append("h2{font-size:14pt;font-weight:bold;margin-top:18pt;margin-bottom:10pt;page-break-after:avoid;}");
        sb.Append(".main p{margin-bottom:10pt;orphans:2;widows:2;text-align:justify;}");
        sb.Append(".sig{text-align:right;margin-top:18pt;white-space:pre-wrap;font-size:11pt;line-height:1.15;}");
        sb.Append(".wm{text-align:center;color:#666;font-size:9pt;margin-top:24pt;font-style:italic;}");
        sb.Append("</style></head><body>");
        sb.Append(CultureInfo.InvariantCulture, $"<h1 class=\"title\">{title}</h1>");
        sb.Append("<div class=\"main\">").Append(main).Append("</div>");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"sig\">{sig}</div>");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"wm\">{wm}</div>");
        sb.Append("</body></html>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] BuildTxtUtf8(StructuredDocumentExportParser.Parts parts, string watermark)
    {
        var sb = new StringBuilder();
        sb.AppendLine(parts.Title);
        sb.AppendLine(new string('=', Math.Min(parts.Title.Length, 72)));
        sb.AppendLine();
        sb.AppendLine(parts.MainText.Trim());
        sb.AppendLine();
        sb.AppendLine(parts.SignatureText.Trim());
        sb.AppendLine();
        sb.AppendLine("—");
        sb.AppendLine(watermark);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EncodeAsHtmlParagraphs(string mainText)
    {
        var t = mainText ?? "";
        var blocks = t.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        foreach (var b in blocks)
        {
            var trimmed = b.Trim();
            if (trimmed.Length == 0)
                continue;
            var encoded = WebUtility.HtmlEncode(trimmed).Replace("\n", "<br/>", StringComparison.Ordinal);
            sb.Append("<p>").Append(encoded).Append("</p>");
        }

        return sb.Length == 0 ? "<p> </p>" : sb.ToString();
    }
}
