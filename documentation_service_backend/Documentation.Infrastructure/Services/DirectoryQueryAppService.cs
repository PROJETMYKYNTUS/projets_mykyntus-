using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Mapping;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Documentation.Infrastructure.Services;

public sealed class DirectoryQueryAppService(DocumentationDbContext db) : IDirectoryQueryAppService
{
    private const string OrgUnitTypePole = "pole";
    private const string OrgUnitTypeCellule = "cellule";
    private const string OrgUnitTypeDepartement = "departement";

    public async Task<IReadOnlyList<DirectoryUserResponse>> ListUsersAsync(CancellationToken ct = default)
    {
        var rows = await db.DirectoryUsers.AsNoTracking()
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return await MapDirectoryUsersAsync(rows, ct);
    }

    public async Task<DirectoryUserResponse?> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.DirectoryUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return row is null ? null : await MapDirectoryUserAsync(row, ct);
    }

    public async Task<IReadOnlyList<OrganizationalUnitSummary>> GetPolesAsync(CancellationToken ct = default)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypePole)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    public async Task<IReadOnlyList<OrganizationalUnitSummary>> GetCellulesByPoleAsync(Guid poleId, CancellationToken ct = default)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypeCellule && u.ParentId == poleId)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    public async Task<IReadOnlyList<OrganizationalUnitSummary>> GetDepartementsByCelluleAsync(Guid celluleId, CancellationToken ct = default)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypeDepartement && u.ParentId == celluleId)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    public async Task<IReadOnlyList<DirectoryUserResponse>> GetUsersByRoleAndOrgAsync(
        string role,
        Guid poleId,
        Guid celluleId,
        Guid departementId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role) || !AppRoleHeaderParser.TryParse(role, out var appRole))
            throw new DocumentationApiException(400, "role invalide (pilote, coach, manager, rp, rh, admin, audit).");

        var rows = await db.DirectoryUsers.AsNoTracking()
            .Where(u =>
                u.Role == appRole &&
                u.PoleId == poleId &&
                u.CelluleId == celluleId &&
                u.DepartementId == departementId)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return await MapDirectoryUsersAsync(rows, ct);
    }

    public async Task<IReadOnlyList<DirectoryUserResponse>> GetManagersByDepartementAsync(Guid departementId, CancellationToken ct = default)
    {
        var rows = await db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Manager && u.DepartementId == departementId)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return await MapDirectoryUsersAsync(rows, ct);
    }

    public async Task<IReadOnlyList<DirectoryUserResponse>> GetCoachesByManagerAsync(
        Guid managerId,
        Guid? departementId,
        CancellationToken ct = default)
    {
        var q = db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Coach && u.ManagerId == managerId);
        if (departementId.HasValue)
            q = q.Where(u => u.DepartementId == departementId.Value);
        var rows = await q.OrderBy(u => u.Nom).ThenBy(u => u.Prenom).ToListAsync(ct);
        return await MapDirectoryUsersAsync(rows, ct);
    }

    public async Task<IReadOnlyList<DirectoryUserResponse>> GetPilotesByCoachAsync(
        Guid coachId,
        Guid? departementId,
        CancellationToken ct = default)
    {
        var q = db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Pilote && u.CoachId == coachId);
        if (departementId.HasValue)
            q = q.Where(u => u.DepartementId == departementId.Value);
        var rows = await q.OrderBy(u => u.Nom).ThenBy(u => u.Prenom).ToListAsync(ct);
        return await MapDirectoryUsersAsync(rows, ct);
    }

    private async Task<IReadOnlyList<DirectoryUserResponse>> MapDirectoryUsersAsync(IReadOnlyList<DirectoryUser> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(u => new[] { u.PoleId, u.CelluleId, u.DepartementId }).Distinct().ToArray();
        var units = await LoadOrgUnitsByIdsAsync(ids, ct);
        return rows.Select(u => DirectoryUserMapper.ToResponse(
            u,
            units.GetValueOrDefault(u.PoleId),
            units.GetValueOrDefault(u.CelluleId),
            units.GetValueOrDefault(u.DepartementId))).ToList();
    }

    private async Task<DirectoryUserResponse> MapDirectoryUserAsync(DirectoryUser row, CancellationToken ct)
    {
        var ids = new[] { row.PoleId, row.CelluleId, row.DepartementId };
        var units = await LoadOrgUnitsByIdsAsync(ids, ct);
        return DirectoryUserMapper.ToResponse(
            row,
            units.GetValueOrDefault(row.PoleId),
            units.GetValueOrDefault(row.CelluleId),
            units.GetValueOrDefault(row.DepartementId));
    }

    private async Task<Dictionary<Guid, OrganisationUnit>> LoadOrgUnitsByIdsAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
            return new Dictionary<Guid, OrganisationUnit>();
        return await db.OrganisationUnits.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }
}
