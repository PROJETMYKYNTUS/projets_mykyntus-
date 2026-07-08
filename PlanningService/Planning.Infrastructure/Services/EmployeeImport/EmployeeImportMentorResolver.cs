using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services.EmployeeImport;

public static class EmployeeImportMentorResolver
{
    public static readonly IReadOnlyList<string> MentorFieldKeys =
        ["chefDeProjetName", "superviseurName", "referentTechniqueName"];

    public static bool HasAnyMentorField(IReadOnlyDictionary<string, string?> mapped) =>
        mapped.Keys.Any(MentorFieldKeys.Contains);

    public static async Task<(Guid? Chef, Guid? Superviseur, Guid? Referent)> ResolveAndValidateAsync(
        AppDbContext db,
        EmployeeImportOrgOverview overview,
        IReadOnlyDictionary<string, string?> mapped,
        string canonicalRole,
        CancellationToken ct = default)
    {
        if (!ShouldValidateMentors(canonicalRole, mapped))
            return (null, null, null);

        if (!HasAnyMentorField(mapped))
            return (null, null, null);

        var (poleId, celluleId, serviceId) = ResolveOrgNodeIds(overview, mapped);
        EnsureOrgReadyForMentors(canonicalRole, mapped, poleId, celluleId, serviceId);

        Guid? chefId = null;
        Guid? superviseurId = null;
        Guid? referentId = null;

        if (mapped.TryGetValue("chefDeProjetName", out var chefName) && !string.IsNullOrWhiteSpace(chefName))
        {
            chefId = await ResolveUserByDisplayNameAsync(db, overview, chefName, ct);
            if (!IsAllowedChef(overview, chefId.Value, poleId))
            {
                throw new InvalidOperationException(
                    "Le chef de projet indiqué n'appartient pas au pôle de l'affectation.");
            }
        }

        if (mapped.TryGetValue("superviseurName", out var supName) && !string.IsNullOrWhiteSpace(supName))
        {
            if (!chefId.HasValue)
            {
                throw new InvalidOperationException(
                    "Indiquez d'abord le chef de projet avant le superviseur.");
            }

            superviseurId = await ResolveUserByDisplayNameAsync(db, overview, supName, ct);
            if (!IsAllowedSuperviseur(overview, superviseurId.Value, chefId.Value, celluleId))
            {
                throw new InvalidOperationException(
                    "Le superviseur indiqué n'est pas rattaché au chef de projet et à la cellule de l'affectation.");
            }
        }

        if (mapped.TryGetValue("referentTechniqueName", out var refName) && !string.IsNullOrWhiteSpace(refName))
        {
            if (!superviseurId.HasValue)
            {
                throw new InvalidOperationException(
                    "Indiquez d'abord le superviseur avant le référent technique.");
            }

            referentId = await ResolveUserByDisplayNameAsync(db, overview, refName, ct);
            if (!IsAllowedReferent(overview, referentId.Value, superviseurId.Value, serviceId))
            {
                throw new InvalidOperationException(
                    "Le référent technique indiqué n'est pas rattaché au superviseur et au service de l'affectation.");
            }
        }

        return (chefId, superviseurId, referentId);
    }

    private static bool ShouldValidateMentors(string canonicalRole, IReadOnlyDictionary<string, string?> mapped)
    {
        if (EmployeeImportRoleSynonymRegistry.GetOrgDepth(canonicalRole) == EmployeeImportOrgDepth.None)
            return false;

        if (EmployeeImportRoleNames.IsChefDeProjet(canonicalRole)
            || EmployeeImportRoleNames.IsSuperviseur(canonicalRole))
            return false;

        return mapped.ContainsKey("pole")
            || mapped.ContainsKey("cellule")
            || mapped.ContainsKey("service")
            || HasAnyMentorField(mapped);
    }

    private static void EnsureOrgReadyForMentors(
        string canonicalRole,
        IReadOnlyDictionary<string, string?> mapped,
        string? poleId,
        string? celluleId,
        string? serviceId)
    {
        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(canonicalRole);
        if (!EmployeeImportRoleSynonymRegistry.HasRequiredOrgColumns(mapped, depth))
        {
            throw new InvalidOperationException(
                $"Complétez l'affectation organisationnelle avant de renseigner les responsables : " +
                $"{EmployeeImportRoleSynonymRegistry.RequiredOrgColumnsMessage(depth)}");
        }

        if (depth == EmployeeImportOrgDepth.Service && string.IsNullOrWhiteSpace(serviceId))
        {
            throw new InvalidOperationException(
                "Service introuvable dans Organisation RH — vérifiez Pôle / Cellule / Service.");
        }

        if ((depth == EmployeeImportOrgDepth.Cellule || depth == EmployeeImportOrgDepth.Service)
            && string.IsNullOrWhiteSpace(celluleId))
        {
            throw new InvalidOperationException(
                "Cellule introuvable dans Organisation RH — vérifiez Pôle et Cellule.");
        }

        if (string.IsNullOrWhiteSpace(poleId))
        {
            throw new InvalidOperationException(
                "Pôle introuvable dans Organisation RH — vérifiez la colonne Pôle.");
        }
    }

    public static (string? PoleId, string? CelluleId, string? ServiceId) ResolveOrgNodeIds(
        EmployeeImportOrgOverview overview,
        IReadOnlyDictionary<string, string?> mapped)
    {
        mapped.TryGetValue("pole", out var pole);
        mapped.TryGetValue("cellule", out var cellule);
        mapped.TryGetValue("service", out var service);

        string? poleId = null;
        if (!string.IsNullOrWhiteSpace(pole))
        {
            poleId = overview.Etages
                .FirstOrDefault(e => Normalize(e.Name) == Normalize(pole))?.Id;
        }

        string? celluleId = null;
        if (!string.IsNullOrWhiteSpace(cellule))
        {
            celluleId = overview.Services
                .FirstOrDefault(s =>
                    Normalize(s.Name) == Normalize(cellule)
                    && (poleId is null || string.Equals(s.EtageId, poleId, StringComparison.OrdinalIgnoreCase)))
                ?.Id;
        }

        string? serviceId = null;
        if (!string.IsNullOrWhiteSpace(service))
        {
            serviceId = overview.SousServices
                .FirstOrDefault(s =>
                    Normalize(s.Name) == Normalize(service)
                    && (celluleId is null || string.Equals(s.ServiceId, celluleId, StringComparison.OrdinalIgnoreCase)))
                ?.Id;
        }

        return (poleId, celluleId, serviceId);
    }

    private static bool IsAllowedChef(EmployeeImportOrgOverview overview, Guid chefGuid, string? poleId)
    {
        if (string.IsNullOrWhiteSpace(poleId)) return false;
        var id = chefGuid.ToString();
        return overview.ManagerEtage.Any(m =>
            string.Equals(m.UserId, id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(m.EtageId, poleId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedSuperviseur(
        EmployeeImportOrgOverview overview,
        Guid superviseurGuid,
        Guid chefGuid,
        string? celluleId)
    {
        var supId = superviseurGuid.ToString();
        var chefId = chefGuid.ToString();

        var fromHierarchy = overview.Employees.Any(e =>
            string.Equals(e.Id, supId, StringComparison.OrdinalIgnoreCase)
            && EmployeeImportRoleNames.IsSuperviseur(e.Role)
            && string.Equals(e.ParentId, chefId, StringComparison.OrdinalIgnoreCase));

        var fromStructure = !string.IsNullOrWhiteSpace(celluleId)
            && overview.SupervisorService.Any(a =>
                string.Equals(a.UserId, supId, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(a.CelluleId, celluleId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.ServiceId, celluleId, StringComparison.OrdinalIgnoreCase)));

        return fromHierarchy || fromStructure;
    }

    private static bool IsAllowedReferent(
        EmployeeImportOrgOverview overview,
        Guid referentGuid,
        Guid superviseurGuid,
        string? serviceId)
    {
        var refId = referentGuid.ToString();
        var supId = superviseurGuid.ToString();

        var fromHierarchy = overview.Employees.Any(e =>
            string.Equals(e.Id, refId, StringComparison.OrdinalIgnoreCase)
            && IsCoachOrReferentRole(e.Role)
            && string.Equals(e.ParentId, supId, StringComparison.OrdinalIgnoreCase));

        var fromStructure = !string.IsNullOrWhiteSpace(serviceId)
            && overview.CoachSousService.Any(a =>
                string.Equals(a.UserId, refId, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(a.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.SousServiceId, serviceId, StringComparison.OrdinalIgnoreCase)));

        return fromHierarchy || fromStructure;
    }

    private static bool IsCoachOrReferentRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return EmployeeImportRoleNames.IsReferentTechnique(role);
    }

    private static async Task<Guid> ResolveUserByDisplayNameAsync(
        AppDbContext db,
        EmployeeImportOrgOverview overview,
        string rawName,
        CancellationToken ct)
    {
        var name = rawName.Trim();
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Nom de responsable vide.");

        var matches = new List<Guid>();

        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Guid, u.FirstName, u.LastName })
            .ToListAsync(ct);

        foreach (var u in users)
        {
            if (DisplayNameMatches(name, u.FirstName, u.LastName))
                matches.Add(u.Guid);
        }

        foreach (var e in overview.Employees)
        {
            if (!DisplayNameMatches(name, e.FirstName, e.LastName))
                continue;
            if (Guid.TryParse(e.Id, out var guid) && !matches.Contains(guid))
                matches.Add(guid);
        }

        matches = matches.Distinct().ToList();
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Aucun employé trouvé pour le responsable « {name} ».");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Plusieurs employés correspondent à « {name} » — précisez le nom complet.");
        }

        return matches[0];
    }

    private static bool DisplayNameMatches(string input, string firstName, string lastName)
    {
        var n = Normalize(input);
        var forward = Normalize($"{firstName} {lastName}");
        var backward = Normalize($"{lastName} {firstName}");
        return n == forward || n == backward;
    }

    private static string Normalize(string value) =>
        EmployeeImportColumnMatcher.Normalize(value);
}
