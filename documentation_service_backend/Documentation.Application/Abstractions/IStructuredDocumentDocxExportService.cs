namespace Documentation.Application.Abstractions;

public interface IStructuredDocumentDocxExportService
{
    byte[] Build(string title, string mainText, string signatureText, string watermark);
}
