using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.Infrastructure.Services.EmployeeImport;

public interface IEmployeeImportStructureAssignmentService
{
    Task ApplyIfNeededAsync(
        Guid employeeGuid,
        string canonicalRoleName,
        IReadOnlyDictionary<string, string?> mapped,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct = default);
}

public sealed class EmployeeImportStructureAssignmentService(
    AppDbContext db,
    IDirectoryOrgWriteClient directoryOrg,
    IEmployeeImportOrgResolver orgResolver,
    ILogger<EmployeeImportStructureAssignmentService> logger) : IEmployeeImportStructureAssignmentService
{
    private List<FloorCacheRow>? _floorsCache;
    private List<ServiceCacheRow>? _servicesCache;
    private Dictionary<int, string?>? _subServicePrimeCache;

    public async Task ApplyIfNeededAsync(
        Guid employeeGuid,
        string canonicalRoleName,
        IReadOnlyDictionary<string, string?> mapped,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct = default)
    {
        if (employeeGuid == Guid.Empty)
            throw new InvalidOperationException("Identifiant employé manquant pour l'affectation structure.");

        if (!NeedsStructureAssignment(canonicalRoleName))
            return;

        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(canonicalRoleName);
        var ok = depth switch
        {
            EmployeeImportOrgDepth.Pole => await AssignChefDeProjetAsync(employeeGuid, mapped, ct),
            EmployeeImportOrgDepth.Cellule => await AssignSuperviseurAsync(employeeGuid, mapped, ct),
            EmployeeImportOrgDepth.Service when EmployeeImportRoleNames.IsReferentTechnique(canonicalRoleName) =>
                await AssignReferentTechniqueAsync(employeeGuid, mapped, snapshot, ct),
            _ => true
        };

        if (!ok)
            throw new InvalidOperationException("Affectation structure échouée.");
    }

    private static bool NeedsStructureAssignment(string canonicalRoleName)
    {
        if (EmployeeImportRoleNames.IsPilote(canonicalRoleName))
            return false;

        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(canonicalRoleName);
        return depth != EmployeeImportOrgDepth.None;
    }

    private async Task<bool> AssignChefDeProjetAsync(
        Guid employeeGuid,
        IReadOnlyDictionary<string, string?> mapped,
        CancellationToken ct)
    {
        mapped.TryGetValue("pole", out var poleName);
        if (string.IsNullOrWhiteSpace(poleName))
            return false;

        var poleDirectoryId = await ResolvePoleDirectoryIdAsync(poleName, ct);
        if (string.IsNullOrWhiteSpace(poleDirectoryId))
        {
            logger.LogWarning("PrimePoleId introuvable pour pôle « {Pole} »", poleName);
            return false;
        }

        return await directoryOrg.AssignChefDeProjetAsync(poleDirectoryId, employeeGuid, ct);
    }

    private async Task<bool> AssignSuperviseurAsync(
        Guid employeeGuid,
        IReadOnlyDictionary<string, string?> mapped,
        CancellationToken ct)
    {
        mapped.TryGetValue("pole", out var poleName);
        mapped.TryGetValue("cellule", out var celluleName);
        if (string.IsNullOrWhiteSpace(poleName) || string.IsNullOrWhiteSpace(celluleName))
            return false;

        var celluleDirectoryId = await ResolveCelluleDirectoryIdAsync(poleName, celluleName, ct);
        if (string.IsNullOrWhiteSpace(celluleDirectoryId))
        {
            logger.LogWarning("PrimeCelluleId introuvable pour cellule « {Cellule} »", celluleName);
            return false;
        }

        return await directoryOrg.AssignSuperviseurAsync(celluleDirectoryId, employeeGuid, ct);
    }

    private async Task<bool> AssignReferentTechniqueAsync(
        Guid employeeGuid,
        IReadOnlyDictionary<string, string?> mapped,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct)
    {
        var subServiceId = orgResolver.ResolveSubServiceId(
            snapshot,
            new Dictionary<string, string?>(mapped));

        if (!subServiceId.HasValue)
            return false;

        var primeCache = await EnsureSubServicePrimeCacheAsync(ct);
        if (!primeCache.TryGetValue(subServiceId.Value, out var primeServiceId) || string.IsNullOrWhiteSpace(primeServiceId))
        {
            logger.LogWarning("PrimeServiceId introuvable pour SubService {Id}", subServiceId);
            return false;
        }

        return await directoryOrg.AssignReferentTechniqueAsync(primeServiceId, employeeGuid, ct);
    }

    private async Task<string?> ResolvePoleDirectoryIdAsync(string poleName, CancellationToken ct)
    {
        var floors = await EnsureFloorsCacheAsync(ct);
        var normalized = EmployeeImportColumnMatcher.Normalize(poleName);
        return floors
            .FirstOrDefault(f => EmployeeImportColumnMatcher.Normalize(f.Name) == normalized)
            ?.PrimePoleId;
    }

    private async Task<string?> ResolveCelluleDirectoryIdAsync(string poleName, string celluleName, CancellationToken ct)
    {
        var services = await EnsureServicesCacheAsync(ct);
        var poleNormalized = EmployeeImportColumnMatcher.Normalize(poleName);
        var cellNormalized = EmployeeImportColumnMatcher.Normalize(celluleName);

        return services
            .FirstOrDefault(s =>
                EmployeeImportColumnMatcher.Normalize(s.FloorName) == poleNormalized
                && EmployeeImportColumnMatcher.Normalize(s.Name) == cellNormalized)
            ?.PrimeCelluleId;
    }

    private async Task<List<FloorCacheRow>> EnsureFloorsCacheAsync(CancellationToken ct)
    {
        if (_floorsCache is not null)
            return _floorsCache;

        _floorsCache = await db.Floors.AsNoTracking()
            .Select(f => new FloorCacheRow(f.Name, f.PrimePoleId))
            .ToListAsync(ct);
        return _floorsCache;
    }

    private async Task<List<ServiceCacheRow>> EnsureServicesCacheAsync(CancellationToken ct)
    {
        if (_servicesCache is not null)
            return _servicesCache;

        _servicesCache = await db.Services.AsNoTracking()
            .Select(s => new ServiceCacheRow(s.Name, s.Floor!.Name, s.PrimeCelluleId))
            .ToListAsync(ct);
        return _servicesCache;
    }

    private async Task<Dictionary<int, string?>> EnsureSubServicePrimeCacheAsync(CancellationToken ct)
    {
        if (_subServicePrimeCache is not null)
            return _subServicePrimeCache;

        _subServicePrimeCache = await db.SubServices.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.PrimeServiceId, ct);
        return _subServicePrimeCache;
    }

    private sealed record FloorCacheRow(string Name, string? PrimePoleId);
    private sealed record ServiceCacheRow(string Name, string FloorName, string? PrimeCelluleId);
}
