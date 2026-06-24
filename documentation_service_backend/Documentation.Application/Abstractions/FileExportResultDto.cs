namespace Documentation.Application.Abstractions;

public sealed record FileExportResultDto(byte[] Content, string ContentType, string FileName);
