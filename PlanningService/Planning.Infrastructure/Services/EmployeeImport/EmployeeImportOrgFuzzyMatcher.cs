namespace Planning.Infrastructure.Services.EmployeeImport;



public sealed class OrgFuzzyResolution

{

    public string? Pole { get; set; }

    public string? Cellule { get; set; }

    public string? Service { get; set; }

    public List<OrgFieldMatchHint> Hints { get; set; } = [];

}



public sealed class OrgFieldMatchHint

{

    public string FieldKey { get; init; } = string.Empty;

    public string SourceValue { get; init; } = string.Empty;

    public string? MatchedValue { get; init; }

    public string Confidence { get; init; } = "low";

    public bool IsNewName { get; init; }

}



public static class EmployeeImportOrgFuzzyMatcher

{

    public static OrgFuzzyResolution ResolveOrgNames(

        EmployeeImportOrgSnapshot snapshot,

        string? pole,

        string? cellule,

        string? service)

    {

        var hints = new List<OrgFieldMatchHint>();



        var resolvedPole = ResolveLevel(snapshot.Rows.Select(r => r.FloorName).Distinct(), pole, "pole", hints);



        IEnumerable<OrgHierarchyRow> afterPole = snapshot.Rows;

        if (!string.IsNullOrWhiteSpace(resolvedPole))

        {

            afterPole = afterPole.Where(r =>

                EmployeeImportColumnMatcher.Normalize(r.FloorName) ==

                EmployeeImportColumnMatcher.Normalize(resolvedPole));

        }



        var resolvedCellule = ResolveLevel(afterPole.Select(r => r.ServiceName).Distinct(), cellule, "cellule", hints);



        IEnumerable<OrgHierarchyRow> afterCellule = afterPole;

        if (!string.IsNullOrWhiteSpace(resolvedCellule))

        {

            afterCellule = afterCellule.Where(r =>

                EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==

                EmployeeImportColumnMatcher.Normalize(resolvedCellule));

        }



        var resolvedService = ResolveLevel(afterCellule.Select(r => r.SubServiceName).Distinct(), service, "service", hints);



        return new OrgFuzzyResolution

        {

            Pole = resolvedPole,

            Cellule = resolvedCellule,

            Service = resolvedService,

            Hints = hints

        };

    }



    private static string? ResolveLevel(

        IEnumerable<string> candidates,

        string? raw,

        string fieldKey,

        List<OrgFieldMatchHint> hints)

    {

        if (string.IsNullOrWhiteSpace(raw))

            return null;



        var candidateList = candidates.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        var exact = candidateList.FirstOrDefault(c =>
            EmployeeImportColumnMatcher.Normalize(c) == EmployeeImportColumnMatcher.Normalize(raw)
            || EmployeeImportOrgNameNormalizer.StripLevelPrefix(c, fieldKey)
            == EmployeeImportOrgNameNormalizer.StripLevelPrefix(raw, fieldKey));



        if (exact is not null)

        {

            hints.Add(new OrgFieldMatchHint

            {

                FieldKey = fieldKey,

                SourceValue = raw,

                MatchedValue = exact,

                Confidence = "high"

            });

            return exact;

        }



        var fuzzy = EmployeeImportFuzzyMatcher.FindBestOrgMatch(fieldKey, raw, candidateList);

        if (fuzzy is not null)

        {

            hints.Add(new OrgFieldMatchHint

            {

                FieldKey = fieldKey,

                SourceValue = raw,

                MatchedValue = fuzzy.Value,

                Confidence = fuzzy.Confidence

            });

            return fuzzy.Value;

        }



        hints.Add(new OrgFieldMatchHint

        {

            FieldKey = fieldKey,

            SourceValue = raw,

            MatchedValue = raw.Trim(),

            Confidence = "low",

            IsNewName = true

        });

        return raw.Trim();

    }



    public static Dictionary<string, string?> ApplyToMapped(Dictionary<string, string?> mapped, OrgFuzzyResolution resolution)

    {

        var copy = new Dictionary<string, string?>(mapped, StringComparer.OrdinalIgnoreCase);

        if (resolution.Pole is not null) copy["pole"] = resolution.Pole;

        if (resolution.Cellule is not null) copy["cellule"] = resolution.Cellule;

        if (resolution.Service is not null) copy["service"] = resolution.Service;

        return copy;

    }

}


