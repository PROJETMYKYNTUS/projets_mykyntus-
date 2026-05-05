using System.Globalization;
using DocumentationBackend.Configuration;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocumentationBackend.Services;

public interface IPdfExportService
{
    /// <summary>
    /// PDF professionnel (même moteur pour aperçu temporaire et document final stocké).
    /// </summary>
    (string FileName, byte[] PdfBytes) BuildPdf(
        string templateCode,
        string tenantId,
        string renderedContent,
        string? documentTitleFallback = null);
}

/// <summary>
/// Mise en page ISO 216 (A4), marges 2,54 cm (1 in), typo lisible type Helvetica/Arial, corps justifié.
/// </summary>
public sealed class PdfExportService(IOptions<DocumentBrandingOptions> brandingOptions) : IPdfExportService
{
    private const float MarginCm = 2.54f;

    /// <summary>Corps : 11 pt, interligne ~1,15 (usage courant documents professionnels).</summary>
    private const float BodyFontPt = 11f;

    private const float BodyLineHeight = 1.15f;

    /// <summary>Titre principal : 16 pt (équivalent h1).</summary>
    private const float TitleFontPt = 16f;

    private readonly DocumentBrandingOptions _brand = brandingOptions.Value;

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public (string FileName, byte[] PdfBytes) BuildPdf(
        string templateCode,
        string tenantId,
        string renderedContent,
        string? documentTitleFallback = null)
    {
        _ = tenantId;
        var now = DateTimeOffset.UtcNow;
        var safeCode = string.IsNullOrWhiteSpace(templateCode) ? "DOC" : templateCode.Trim();
        var fileName = $"GEN_{safeCode}_{now:yyyyMMddHHmmss}.pdf";
        var fallback = string.IsNullOrWhiteSpace(documentTitleFallback) ? "DOCUMENT" : documentTitleFallback.Trim();
        var parts = StructuredDocumentExportParser.Parse(renderedContent ?? "", fallback);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(MarginCm, Unit.Centimetre);
                page.MarginBottom(MarginCm, Unit.Centimetre);
                page.MarginLeft(MarginCm, Unit.Centimetre);
                page.MarginRight(MarginCm, Unit.Centimetre);
                page.DefaultTextStyle(x => x
                    .FontFamily(Fonts.Arial)
                    .FontSize(BodyFontPt)
                    .FontColor(Colors.Black));

                page.Footer().AlignCenter().PaddingTop(8).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontFamily(Fonts.Arial).FontSize(9f).FontColor(Colors.Grey.Medium));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12f);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Row(left =>
                        {
                            var logo = _brand.LogoPath?.Trim();
                            if (!string.IsNullOrEmpty(logo) && File.Exists(logo))
                            {
                                left.ConstantItem(56).Height(40).Image(logo).FitArea();
                                left.ConstantItem(12);
                            }

                            left.RelativeItem().Column(h =>
                            {
                                h.Item().Text(_brand.CompanyName)
                                    .FontSize(10f)
                                    .SemiBold();
                            });
                        });

                        row.RelativeItem().AlignRight().Text(BuildPlaceDateLine())
                            .FontSize(9f)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    column.Item().PaddingTop(24).Text(parts.Title.ToUpperInvariant())
                        .FontSize(TitleFontPt)
                        .Bold()
                        .AlignCenter();

                    foreach (var block in SplitParagraphs(parts.MainText))
                    {
                        column.Item().PaddingBottom(10).PaddingTop(2).Text(block)
                            .FontSize(BodyFontPt)
                            .LineHeight(BodyLineHeight)
                            .Justify();
                    }

                    column.Item().PaddingTop(14).AlignRight().Text(BuildPlaceDateLine("dd/MM/yyyy"))
                        .FontSize(10f)
                        .FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingTop(8).AlignRight().Column(sigCol =>
                    {
                        sigCol.Spacing(2f);
                        foreach (var line in SplitSignatureLines(parts.SignatureText))
                        {
                            sigCol.Item().Text(line)
                                .FontSize(BodyFontPt)
                                .LineHeight(BodyLineHeight)
                                .AlignRight();
                        }
                    });

                    column.Item().PaddingTop(32).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    column.Item().PaddingTop(8).Column(footerCol =>
                    {
                        footerCol.Spacing(4f);
                        foreach (var line in BuildFooterLines())
                        {
                            footerCol.Item().AlignCenter().Text(line)
                                .FontSize(8f)
                                .FontColor(Colors.Grey.Darken1)
                                .LineHeight(1.2f);
                        }
                    });
                });
            });
        }).GeneratePdf();

        return (fileName, pdf);
    }

    /// <summary>Mention de lieu et date (alignée à droite dans l’en-tête).</summary>
    private string BuildPlaceDateLine(string format = "d MMMM yyyy")
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var now = DateTime.Now;
        var datePart = now.ToString(format, culture);
        if (datePart.Length > 0)
            datePart = char.ToUpperInvariant(datePart[0]) + datePart.Substring(1);
        var city = _brand.DocumentCity?.Trim();
        return string.IsNullOrEmpty(city)
            ? $"Le {datePart}"
            : $"Fait à {city}, le {datePart}";
    }

    private IReadOnlyList<string> BuildFooterLines()
    {
        static string Cell(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return $"{label} : {value.Trim()}";
        }

        var bits = new[]
        {
            Cell("ICE", _brand.Ice),
            Cell("IF", _brand.IdentifiantFiscal),
            Cell("CNSS", _brand.Cnss),
            Cell("RC", _brand.Rc),
            Cell("E-mail", _brand.ContactEmail),
            Cell("Tél", _brand.ContactPhone),
        }.Where(s => !string.IsNullOrEmpty(s)).ToList();

        if (bits.Count == 0)
            return new[] { _brand.CompanyName };

        // Répartition lisible (évite une seule ligne « entassée »).
        var mid = (bits.Count + 1) / 2;
        var line1 = string.Join("    ", bits.Take(mid));
        var line2 = string.Join("    ", bits.Skip(mid));
        return string.IsNullOrWhiteSpace(line2) ? new[] { line1 } : new[] { line1, line2 };
    }

    private static IEnumerable<string> SplitParagraphs(string mainText)
    {
        var t = string.IsNullOrWhiteSpace(mainText) ? " " : mainText.Trim();
        var parts = t.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.None);
        foreach (var p in parts)
        {
            var s = p.Trim();
            yield return string.IsNullOrEmpty(s) ? " " : s.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> SplitSignatureLines(string signatureText)
    {
        var raw = string.IsNullOrWhiteSpace(signatureText)
            ? "La Gérante\nMme RABHA HERRAS\n(Signature et cachet)"
            : signatureText;
        foreach (var line in raw.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            yield return line;
        }
    }
}
