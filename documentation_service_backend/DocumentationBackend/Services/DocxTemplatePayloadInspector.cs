using System.IO.Compression;
using DocumentationBackend.Application.Abstractions;

namespace DocumentationBackend.Services;

/// <summary>
/// Détecte un binaire « Word Open XML » (.docx) même si le nom d’objet S3 / l’URI n’inclut pas l’extension
/// (cas fréquent : clé = GUID), afin d’utiliser le rendu fidèle au fichier uploadé au lieu du PDF générique.
/// </summary>
public static class DocxTemplatePayloadInspector
{
    public static bool IsWordProcessingOpenXml(TemplateBlobPayload? payload)
    {
        if (payload is null || payload.Content.Length < 4)
            return false;

        var ct = payload.ContentType ?? string.Empty;
        if (ct.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase) &&
            !ct.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase))
            return true;

        var fn = payload.FileName ?? string.Empty;
        if (fn.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return true;

        // .doc binaire OLE — pas géré par Open XML.
        if (fn.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) &&
            !fn.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return false;

        var b = payload.Content;
        if (b[0] != (byte)'P' || b[1] != (byte)'K')
            return false;

        try
        {
            using var ms = new MemoryStream(b, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            return zip.GetEntry("word/document.xml") is not null;
        }
        catch
        {
            return false;
        }
    }
}
