namespace Documentation.Application.Api;

public sealed class AuditLogListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Action { get; init; }
    public string? Role { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}
