using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public static class EmployeeImportOrgExistence
{
    public static bool PoleExists(EmployeeImportOrgSnapshot snapshot, string pole) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole));

    public static bool CelluleExists(EmployeeImportOrgSnapshot snapshot, string pole, string cellule) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole)
            && EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==
            EmployeeImportColumnMatcher.Normalize(cellule));

    public static bool ServiceExists(
        EmployeeImportOrgSnapshot snapshot,
        string pole,
        string cellule,
        string service) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole)
            && EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==
            EmployeeImportColumnMatcher.Normalize(cellule)
            && EmployeeImportColumnMatcher.Normalize(r.SubServiceName) ==
            EmployeeImportColumnMatcher.Normalize(service));

    public static bool AlreadyExists(PendingOrgCreationDto item, EmployeeImportOrgSnapshot snapshot) =>
        item.Type switch
        {
            "pole" => !string.IsNullOrWhiteSpace(item.Pole) && PoleExists(snapshot, item.Pole),
            "cellule" => !string.IsNullOrWhiteSpace(item.Pole) && !string.IsNullOrWhiteSpace(item.Cellule)
                && CelluleExists(snapshot, item.Pole, item.Cellule),
            "service" => !string.IsNullOrWhiteSpace(item.Pole) && !string.IsNullOrWhiteSpace(item.Cellule)
                && !string.IsNullOrWhiteSpace(item.Service)
                && ServiceExists(snapshot, item.Pole, item.Cellule, item.Service),
            _ => false
        };

    public static (List<PendingOrgCreationDto> Needed, List<string> SkippedLabels) FilterStillNeeded(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot)
    {
        var needed = new List<PendingOrgCreationDto>();
        var skipped = new List<string>();

        foreach (var item in approved)
        {
            if (AlreadyExists(item, snapshot))
            {
                skipped.Add(FormatCreationLabel(item));
                continue;
            }

            needed.Add(item);
        }

        return (needed, skipped);
    }

    public static void ValidateNoDuplicateCreations(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot)
    {
        foreach (var item in approved)
        {
            if (AlreadyExists(item, snapshot))
                continue;

            var (fieldKey, raw, candidates) = GetDuplicateCheckContext(item, snapshot);
            if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(raw))
                continue;

            var likely = EmployeeImportFuzzyMatcher.FindBestOrgMatch(fieldKey, raw, candidates);
            if (likely is null)
                continue;

            var levelLabel = OrgLevelLabel(fieldKey);
            throw new InvalidOperationException(
                $"Création refusée : un {levelLabel} très proche existe déjà (« {likely.Value} »). " +
                $"Utilisez le nom existant ou validez la correspondance proposée à l'étape Organisation.");
        }
    }

    public static string FormatCreationLabel(PendingOrgCreationDto item) =>
        item.Type switch
        {
            "pole" => $"pôle:{item.Pole}",
            "cellule" => $"cellule:{item.Pole}|{item.Cellule}",
            "service" => $"service:{item.Pole}|{item.Cellule}|{item.Service}",
            _ => item.Type
        };

    private static (string FieldKey, string? Raw, List<string> Candidates) GetDuplicateCheckContext(
        PendingOrgCreationDto item,
        EmployeeImportOrgSnapshot snapshot) =>
        item.Type switch
        {
            "pole" => ("pole", item.Pole, snapshot.Rows.Select(r => r.FloorName).Distinct().ToList()),
            "cellule" => ("cellule", item.Cellule, FilterRows(snapshot.Rows, item.Pole, null, null)
                .Select(r => r.ServiceName).Distinct().ToList()),
            "service" => ("service", item.Service, FilterRows(snapshot.Rows, item.Pole, item.Cellule, null)
                .Select(r => r.SubServiceName).Distinct().ToList()),
            _ => (string.Empty, null, [])
        };

    private static string OrgLevelLabel(string typeOrFieldKey) => typeOrFieldKey switch
    {
        "pole" => "pôle",
        "cellule" => "cellule",
        _ => "service"
    };

    private static List<OrgHierarchyRow> FilterRows(
        IReadOnlyList<OrgHierarchyRow> rows,
        string? pole,
        string? cellule,
        string? service)
    {
        IEnumerable<OrgHierarchyRow> q = rows;
        if (!string.IsNullOrWhiteSpace(pole))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.FloorName) == EmployeeImportColumnMatcher.Normalize(pole));
        if (!string.IsNullOrWhiteSpace(cellule))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.ServiceName) == EmployeeImportColumnMatcher.Normalize(cellule));
        if (!string.IsNullOrWhiteSpace(service))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.SubServiceName) == EmployeeImportColumnMatcher.Normalize(service));
        return q.ToList();
    }
}
