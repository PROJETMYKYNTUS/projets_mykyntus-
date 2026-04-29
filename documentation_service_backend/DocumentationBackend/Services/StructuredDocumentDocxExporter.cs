using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentationBackend.Services;

/// <summary>Export Word : A4, marges 2,54 cm, Arial 11 pt / titre 16 pt, interligne ~1,15, corps justifié.</summary>
public static class StructuredDocumentDocxExporter
{
    private const uint MarginTwips = 1440;

    private const uint PageWidthTwips = 11906;

    private const uint PageHeightTwips = 16838;

    /// <summary>Interligne multiple ~1,15 (276 = 1,15 × 240).</summary>
    private const string LineSpacing = "276";

    public static byte[] Build(string title, string mainText, string signatureText, string footerOfficial)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.AppendChild(CreateTitleParagraph(title));
            body.AppendChild(new Paragraph(new Run(new Text(" "))));

            foreach (var block in SplitBlocks(mainText))
                body.AppendChild(CreateJustifiedParagraph(block));

            body.AppendChild(new Paragraph(new Run(new Text(" "))));
            body.AppendChild(CreateSignatureParagraph(signatureText));

            var footerPart = mainPart.AddNewPart<FooterPart>();
            var footerId = mainPart.GetIdOfPart(footerPart);
            footerPart.Footer = new Footer(CreateFooterParagraph(footerOfficial));

            body.AppendChild(new SectionProperties(
                new PageSize { Width = PageWidthTwips, Height = PageHeightTwips },
                new PageMargin
                {
                    Top = (int)MarginTwips,
                    Bottom = (int)MarginTwips,
                    Left = MarginTwips,
                    Right = MarginTwips,
                },
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId }));
        }

        return ms.ToArray();
    }

    private static IEnumerable<string> SplitBlocks(string text)
    {
        var t = string.IsNullOrWhiteSpace(text) ? " " : text;
        var parts = t.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None);
        foreach (var p in parts)
        {
            var s = p.Trim();
            yield return string.IsNullOrEmpty(s) ? " " : s;
        }
    }

    private static Paragraph CreateTitleParagraph(string title)
    {
        var safe = string.IsNullOrWhiteSpace(title) ? "Document" : title.Trim();
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                    new Bold(),
                    new FontSize { Val = "32" }),
                new Text(safe) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph CreateJustifiedParagraph(string block)
    {
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Both },
                new SpacingBetweenLines { Line = LineSpacing, LineRule = LineSpacingRuleValues.Auto }),
            CreateMultiLineRun(block, bodyParagraph: true));
    }

    private static Paragraph CreateSignatureParagraph(string signature)
    {
        var safe = string.IsNullOrWhiteSpace(signature) ? " " : signature.Trim();
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Right },
                new SpacingBetweenLines { Line = LineSpacing, LineRule = LineSpacingRuleValues.Auto }),
            CreateMultiLineRun(safe, bodyParagraph: true));
    }

    private static Paragraph CreateFooterParagraph(string text)
    {
        var safe = string.IsNullOrWhiteSpace(text) ? "Officiel" : text.Trim();
        return new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                    new Italic(),
                    new Color { Val = "666666" },
                    new FontSize { Val = "18" }),
                new Text(safe) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Run CreateMultiLineRun(string text, bool bodyParagraph)
    {
        var run = new Run();
        if (bodyParagraph)
        {
            run.AppendChild(new RunProperties(
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                new FontSize { Val = "22" }));
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.AppendChild(new Break());
            var line = lines[i];
            run.AppendChild(new Text(string.IsNullOrEmpty(line) ? " " : line)
            {
                Space = SpaceProcessingModeValues.Preserve,
            });
        }

        return run;
    }
}
