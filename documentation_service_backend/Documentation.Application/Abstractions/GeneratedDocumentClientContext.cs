namespace Documentation.Application.Abstractions;

/// <summary>Contexte client pour l'audit des téléchargements (adresse IP, user-agent).</summary>
public sealed record GeneratedDocumentClientContext(string? RemoteIpAddress, string? UserAgent);
