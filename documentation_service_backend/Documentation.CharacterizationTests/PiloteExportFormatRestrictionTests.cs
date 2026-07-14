using Documentation.Domain.Entities;

namespace DocumentationBackend.CharacterizationTests;

/// <summary>
/// Spec de la restriction d’export (miroir de GeneratedDocumentAppService) :
/// le profil Pilote ne peut télécharger que le PDF.
/// </summary>
public class PiloteExportFormatRestrictionTests
{
    [Theory]
    [InlineData(AppRole.Pilote, "docx", true)]
    [InlineData(AppRole.Pilote, "pdf", false)]
    [InlineData(AppRole.Pilote, "txt", true)]
    [InlineData(AppRole.Pilote, "html", true)]
    [InlineData(AppRole.Rh, "docx", false)]
    [InlineData(AppRole.Admin, "docx", false)]
    [InlineData(AppRole.Audit, "docx", false)]
    [InlineData(AppRole.Coach, "docx", false)]
    public void Pilote_is_blocked_from_non_pdf_exports(AppRole role, string format, bool shouldBlock)
    {
        var blocked = IsBlockedForRole(role, format);
        Assert.Equal(shouldBlock, blocked);
    }

    /// <summary>Même règle que <c>GeneratedDocumentAppService.ExportGeneratedDocumentCoreAsync</c>.</summary>
    private static bool IsBlockedForRole(AppRole role, string format) =>
        format is "docx" or "txt" or "html" && role is AppRole.Pilote;
}
