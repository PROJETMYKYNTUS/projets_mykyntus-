using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Infrastructure.Persistence;
using Kyntus.Iam;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class DirectoryIamReadService(
    DirectoryDbContext db,
    IOrgResponsibilityResolver responsibility) : IIamReadService, IPermissionCatalog, IRebacClient
{
    public async Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(Guid subjectId, string role, CancellationToken ct = default)
    {
        var keys = await GetEffectivePermissionKeysAsync(role, ct);
        return new EffectivePermissionsDto(subjectId.ToString(), role, keys);
    }

    public async Task<bool> EvaluateAsync(Guid subjectId, string role, string action, string resourceType, string? resourceId, CancellationToken ct = default)
    {
        var evaluator = new PolicyEvaluator(this, this, Microsoft.Extensions.Logging.Abstractions.NullLogger<PolicyEvaluator>.Instance);
        var decision = await evaluator.EvaluateAsync(new PolicyRequest(subjectId, role, action, resourceType, resourceId), ct);
        return decision.Allowed;
    }

    public Task<bool> RoleHasActionAsync(string role, string action, string scope, CancellationToken ct = default) =>
        db.IamPermissions.AsNoTracking()
            .AnyAsync(p => p.Role == role.Trim() && p.Action == action.Trim() && p.Scope == scope.Trim() && p.IsAllowed, ct);

    public async Task<IReadOnlyList<string>> GetEffectivePermissionKeysAsync(string role, CancellationToken ct = default)
    {
        var rows = await db.IamPermissions.AsNoTracking()
            .Where(p => p.Role == role.Trim() && p.IsAllowed)
            .Select(p => p.Action + ":" + p.Scope)
            .Distinct()
            .ToListAsync(ct);
        return rows;
    }

    public async Task<bool> IsDescendantAsync(Guid viewerId, Guid targetId, CancellationToken ct = default)
    {
        var read = new DirectoryReadService(db);
        return await read.IsDescendantAsync(viewerId, targetId, ct);
    }

    public async Task<IReadOnlyList<string>> GetManagedNodeIdsAsync(Guid employeeId, string kind, CancellationToken ct = default)
    {
        var read = new DirectoryReadService(db);
        var dto = await read.GetManagedNodesAsync(employeeId, kind, ct);
        return dto.NodeIds;
    }

    public Task<IReadOnlyList<Guid>> GetManagedEmployeeIdsAsync(Guid actorId, CancellationToken ct = default) =>
        responsibility.GetManagedEmployeeIdsAsync(actorId, ct);

    public Task<bool> CanActOnAsync(Guid actorId, Guid targetEmployeeId, CancellationToken ct = default) =>
        responsibility.CanActOnAsync(actorId, targetEmployeeId, ct);

    public async Task<IReadOnlyList<Guid>> GetResponsibleIdsAsync(string kind, string nodeId, CancellationToken ct = default)
    {
        var list = await responsibility.GetResponsiblesAsync(kind, nodeId, ct);
        return list
            .Select(r => Guid.TryParse(r.EmployeeId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();
    }
}
