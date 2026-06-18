namespace PlanningService.Services.EmployeeImport;

public static class EmployeeImportLevelResolver
{
    public const int DefaultLevel = 1;

    public static readonly IReadOnlyList<string> Labels = ["Débutant", "Intermédiaire", "Expert"];

    public static int Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultLevel;

        if (TryResolve(raw, out var level))
            return level;

        throw new InvalidOperationException(
            "Niveau invalide : utilisez Débutant, Intermédiaire ou Expert.");
    }

    public static bool TryResolve(string? raw, out int level)
    {
        level = DefaultLevel;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, out var num) && num is >= 1 and <= 3)
        {
            level = num;
            return true;
        }

        var normalized = EmployeeImportColumnMatcher.Normalize(trimmed);
        switch (normalized)
        {
            case "debutant" or "beginner":
                level = 1;
                return true;
            case "intermediaire" or "intermediate":
                level = 2;
                return true;
            case "expert" or "senior":
                level = 3;
                return true;
            default:
                return false;
        }
    }
}
