using PlanningService.DTOs;

namespace PlanningService.Services.EmployeeImport;

public static class EmployeeImportMappingHelper
{
    public static Dictionary<int, string> BuildColumnMap(
        IReadOnlyList<EmployeeImportMappingItemDto> mappings,
        IReadOnlyList<EmployeeImportFieldConfigDto> activeFields)
    {
        var enabledKeys = activeFields
            .Where(f => f.IsEnabled)
            .Select(f => f.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<int, string>();
        foreach (var m in mappings)
        {
            if (string.IsNullOrWhiteSpace(m.FieldKey))
                continue;
            if (!enabledKeys.Contains(m.FieldKey))
                continue;
            map[m.ColumnIndex] = m.FieldKey;
        }

        return map;
    }
}
