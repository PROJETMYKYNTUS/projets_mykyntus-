using Formation.Domain.Entities;
using Formation.Domain.Enums;

namespace Formation.Infrastructure.Services;

public static class AudienceResolver
{
    public static bool Matches(
        EmployeAnnuaire e,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> structures,
        IReadOnlyList<Guid> users,
        CatalogAudienceMatchMode mode)
    {
        var roleOk = roles.Count == 0
            || roles.Any(r => e.Role.Equals(r, StringComparison.OrdinalIgnoreCase));
        var structureOk = MatchStructure(e, structures);
        var userOk = users.Count == 0 || users.Contains(e.EmployeId);

        // Dimensions with empty filter are ignored.
        var checks = new List<bool>();
        if (roles.Count > 0) checks.Add(roleOk);
        if (structures.Count > 0) checks.Add(structureOk);
        if (users.Count > 0) checks.Add(userOk);
        if (checks.Count == 0) return true;

        return mode == CatalogAudienceMatchMode.MatchAll
            ? checks.All(x => x)
            : checks.Any(x => x);
    }

    public static bool MatchStructure(EmployeAnnuaire e, IReadOnlyList<string> structures)
    {
        if (structures.Count == 0) return true;
        // Un nœud sélectionné matche si égal à n'importe quel niveau du chemin employé
        // (sélectionner un pôle inclut tous les employés de ce pôle)
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void add(string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) keys.Add(v.Trim());
        }
        add(e.DepartmentId);
        add(e.PoleId);
        add(e.CelluleId);
        add(e.ServiceId);
        add(e.StructureKey); // legacy
        return structures.Any(s => !string.IsNullOrWhiteSpace(s) && keys.Contains(s.Trim()));
    }
}
