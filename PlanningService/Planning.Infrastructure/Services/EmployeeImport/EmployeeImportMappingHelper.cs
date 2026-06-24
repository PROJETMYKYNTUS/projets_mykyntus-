using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

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
            if (IsIgnored(m))
                continue;
            if (string.IsNullOrWhiteSpace(m.FieldKey))
                continue;
            if (!enabledKeys.Contains(m.FieldKey))
                continue;
            map[m.ColumnIndex] = m.FieldKey;
        }

        return map;
    }

    public static bool IsIgnored(EmployeeImportMappingItemDto mapping)
    {
        if (string.Equals(mapping.Disposition, "ignore", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(mapping.Disposition, "keepAsNewField", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(mapping.FieldKey);
        return string.IsNullOrWhiteSpace(mapping.FieldKey);
    }
}
