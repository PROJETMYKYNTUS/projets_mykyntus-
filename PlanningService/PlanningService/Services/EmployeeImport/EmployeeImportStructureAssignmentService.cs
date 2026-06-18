using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Services;

namespace PlanningService.Services.EmployeeImport;

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

        var primeServiceId = await db.SubServices.AsNoTracking()
            .Where(s => s.Id == subServiceId.Value)
            .Select(s => s.PrimeServiceId)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(primeServiceId))
        {
            logger.LogWarning("PrimeServiceId introuvable pour SubService {Id}", subServiceId);
            return false;
        }

        return await directoryOrg.AssignReferentTechniqueAsync(primeServiceId, employeeGuid, ct);
    }

    private async Task<string?> ResolvePoleDirectoryIdAsync(string poleName, CancellationToken ct)
    {
        var normalized = EmployeeImportColumnMatcher.Normalize(poleName);
        var floors = await db.Floors.AsNoTracking().ToListAsync(ct);
        return floors
            .FirstOrDefault(f => EmployeeImportColumnMatcher.Normalize(f.Name) == normalized)
            ?.PrimePoleId;
    }

    private async Task<string?> ResolveCelluleDirectoryIdAsync(string poleName, string celluleName, CancellationToken ct)
    {
        var poleNormalized = EmployeeImportColumnMatcher.Normalize(poleName);
        var cellNormalized = EmployeeImportColumnMatcher.Normalize(celluleName);

        var services = await db.Services.AsNoTracking()
            .Include(s => s.Floor)
            .ToListAsync(ct);

        return services
            .FirstOrDefault(s =>
                s.Floor is not null
                && EmployeeImportColumnMatcher.Normalize(s.Floor.Name) == poleNormalized
                && EmployeeImportColumnMatcher.Normalize(s.Name) == cellNormalized)
            ?.PrimeCelluleId;
    }
}
