namespace Parrainage.Application.Abstractions;

public interface IOrgHierarchyQueryService
{
    Task<IReadOnlyList<OrgNodeDto>> ListNodesAsync(CancellationToken ct = default);
}

public sealed record OrgNodeDto(string Id, string? ParentId, string Email, string Role, string Name);
