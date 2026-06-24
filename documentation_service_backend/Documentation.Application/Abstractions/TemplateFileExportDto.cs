namespace Documentation.Application.Abstractions;

public sealed record TemplateFileExportDto(
    byte[] Content,
    string ContentType,
    string FileName,
    IReadOnlyDictionary<string, string>? ResponseHeaders = null);
