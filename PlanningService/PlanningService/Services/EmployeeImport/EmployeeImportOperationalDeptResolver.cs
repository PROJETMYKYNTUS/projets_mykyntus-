using PlanningService.Services;

namespace PlanningService.Services.EmployeeImport;

public static class EmployeeImportOperationalDeptResolver
{
    public static Guid? ResolveBusinessDepartmentId(
        string? operationalDepartmentName,
        IReadOnlyList<DirectoryOperationalDepartmentJson> departments)
    {
        if (string.IsNullOrWhiteSpace(operationalDepartmentName) || departments.Count == 0)
            return null;

        var normalized = EmployeeImportColumnMatcher.Normalize(operationalDepartmentName);
        var matches = departments.Where(d =>
            EmployeeImportColumnMatcher.Normalize(d.Name) == normalized
            || EmployeeImportColumnMatcher.Normalize(d.Code) == normalized
            || EmployeeImportColumnMatcher.Normalize($"{d.Code} {d.Name}") == normalized
            || EmployeeImportColumnMatcher.Normalize($"{d.Name} {d.Code}") == normalized).ToList();

        if (matches.Count == 1 && Guid.TryParse(matches[0].Id, out var single))
            return single;

        return null;
    }

    public static Guid? ResolveUniqueByPoleName(
        string poleName,
        IReadOnlyList<DirectoryOperationalDepartmentJson> departments,
        IReadOnlyList<EmployeeImportOperationalPoleRef> poles)
    {
        if (string.IsNullOrWhiteSpace(poleName) || poles.Count == 0)
            return null;

        var normalized = EmployeeImportColumnMatcher.Normalize(poleName);
        var matches = poles
            .Where(p => EmployeeImportColumnMatcher.Normalize(p.Name) == normalized)
            .Select(p => p.BusinessDepartmentId)
            .Distinct()
            .ToList();

        if (matches.Count != 1 || !Guid.TryParse(matches[0], out var deptId))
            return null;

        return departments.Any(d => d.Id == matches[0]) ? deptId : null;
    }
}

public sealed class EmployeeImportOperationalPoleRef
{
    public string Name { get; init; } = "";
    public string BusinessDepartmentId { get; init; } = "";
}
