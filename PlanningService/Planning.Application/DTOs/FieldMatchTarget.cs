namespace Planning.Application.DTOs;

public sealed record FieldMatchTarget(string FieldKey, string Label, IReadOnlyList<string> Aliases);
