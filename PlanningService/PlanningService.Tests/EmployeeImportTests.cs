using PlanningService.Services.EmployeeImport;
using Xunit;

namespace PlanningService.Tests;

public class EmployeeImportColumnMatcherTests
{
    private readonly EmployeeImportColumnMatcher _matcher = new();

    [Theory]
    [InlineData("Prénom", "firstName")]
    [InlineData("Email *", "email")]
    [InlineData("Nom de famille", "lastName")]
    [InlineData("Role", "role")]
    [InlineData("Pôle", "pole")]
    [InlineData("Cellule", "cellule")]
    public void MatchHeaders_maps_org_columns(string header, string expectedField)
    {
        var fields = EmployeeImportFieldRegistry.DefaultFields
            .Select(f => new FieldMatchTarget(f.FieldKey, f.Label, f.Aliases))
            .ToList();

        var result = _matcher.MatchHeaders([header], fields);
        Assert.Equal(expectedField, result[0].SuggestedFieldKey);
    }

    [Fact]
    public void Normalize_strips_accents_and_punctuation()
    {
        var normalized = EmployeeImportColumnMatcher.Normalize("Date d'embauche *");
        Assert.Contains("embauche", normalized);
    }

    [Fact]
    public void IsIgnorableHeader_detects_empty_and_placeholder_columns()
    {
        Assert.True(EmployeeImportColumnMatcher.IsIgnorableHeader(""));
        Assert.True(EmployeeImportColumnMatcher.IsIgnorableHeader("   "));
        Assert.True(EmployeeImportColumnMatcher.IsIgnorableHeader("Colonne 3"));
        Assert.False(EmployeeImportColumnMatcher.IsIgnorableHeader("Email"));
    }
}

public class EmployeeImportRowMapperTests
{
    [Fact]
    public void MapRow_ignores_unmapped_column_indices()
    {
        var row = new[] { "a@test.ma", "IGNORED", "Dupont", "extra" };
        var map = new Dictionary<int, string>
        {
            [0] = "email",
            [2] = "lastName",
        };

        var result = EmployeeImportRowMapper.MapRow(row, map);
        Assert.Equal("a@test.ma", result["email"]);
        Assert.Equal("Dupont", result["lastName"]);
        Assert.False(result.ContainsKey("firstName"));
    }

    [Fact]
    public void MapRow_ignores_empty_cells()
    {
        var row = new[] { "a@test.ma", "", "Dupont" };
        var map = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName"
        };

        var result = EmployeeImportRowMapper.MapRow(row, map);
        Assert.Equal("a@test.ma", result["email"]);
        Assert.False(result.ContainsKey("firstName"));
        Assert.Equal("Dupont", result["lastName"]);
    }

    [Theory]
    [InlineData("non", false)]
    [InlineData("Oui", true)]
    [InlineData("", false)]
    public void TryParseBool_parses_french_values(string input, bool expected)
    {
        var ok = EmployeeImportRowMapper.TryParseBool(input, out var result);
        if (string.IsNullOrWhiteSpace(input))
        {
            Assert.False(ok);
            return;
        }
        Assert.True(ok);
        Assert.Equal(expected, result);
    }
}

public class EmployeeImportFieldRegistryTests
{
    [Fact]
    public void TemplateFields_has_twelve_columns_including_operational_department()
    {
        Assert.Equal(12, EmployeeImportFieldRegistry.TemplateFields.Count);
        Assert.Contains(EmployeeImportFieldRegistry.TemplateFields, f => f.FieldKey == "operationalDepartment");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    public void IsAdminRoleName_detects_admin(string roleName)
    {
        Assert.True(EmployeeImportFieldRegistry.IsAdminRoleName(roleName));
    }
}

public class EmployeeImportLevelResolverTests
{
    [Theory]
    [InlineData("Débutant", 1)]
    [InlineData("debutant", 1)]
    [InlineData("Intermédiaire", 2)]
    [InlineData("intermediaire", 2)]
    [InlineData("Expert", 3)]
    [InlineData("3", 3)]
    public void TryResolve_maps_labels_to_level(string input, int expected)
    {
        Assert.True(EmployeeImportLevelResolver.TryResolve(input, out var level));
        Assert.Equal(expected, level);
    }

    [Fact]
    public void Resolve_empty_defaults_to_debutant()
    {
        Assert.Equal(1, EmployeeImportLevelResolver.Resolve(null));
        Assert.Equal(1, EmployeeImportLevelResolver.Resolve(""));
    }

    [Fact]
    public void Resolve_invalid_throws()
    {
        Assert.Throws<InvalidOperationException>(() => EmployeeImportLevelResolver.Resolve("Confirmé"));
    }
}

public class EmployeeImportRoleSynonymTests
{
    [Theory]
    [InlineData("pilote", "Pilote")]
    [InlineData("employé", "Pilote")]
    [InlineData("coach", "Référent technique")]
    [InlineData("superviseur", "Superviseur")]
    [InlineData("rp", "Chef de projet")]
    public void TryResolveCanonicalRole_maps_synonyms(string input, string expected)
    {
        Assert.True(EmployeeImportRoleSynonymRegistry.TryResolveCanonicalRole(input, out var canonical));
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("manager")]
    [InlineData("Manager")]
    public void IsImportForbiddenRoleName_blocks_admin_and_manager(string role)
    {
        Assert.True(EmployeeImportFieldRegistry.IsImportForbiddenRoleName(role));
    }

    [Fact]
    public void ResolveRole_manager_is_forbidden()
    {
        var roles = new List<PlanningService.Models.Role>
        {
            new() { Id = 1, Name = "Manager" },
            new() { Id = 2, Name = "Superviseur" },
        };

        var result = EmployeeImportRoleResolver.Resolve("manager", roles);
        Assert.Contains("Manager", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ResolveRole_fuzzy_does_not_map_manager_to_superviseur()
    {
        var roles = new List<PlanningService.Models.Role>
        {
            new() { Id = 1, Name = "Superviseur" },
            new() { Id = 2, Name = "Pilote" },
        };

        var result = EmployeeImportRoleResolver.Resolve("manager", roles);
        Assert.Null(result.CanonicalRoleName);
        Assert.NotEqual("Superviseur", result.CanonicalRoleName);
    }
}

public class EmployeeImportFuzzyMatcherTests
{
    [Fact]
    public void OrgFuzzyMatcher_strips_service_prefix_and_matches_existing()
    {
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows =
            [
                new OrgHierarchyRow(42, "Satisfaction client", 1, "Support", 1, "Pôle A")
            ]
        };

        var resolution = EmployeeImportOrgFuzzyMatcher.ResolveOrgNames(
            snapshot, "Pôle A", "Support", "service satisfaction client");

        Assert.Equal("Satisfaction client", resolution.Service);
        var serviceHint = resolution.Hints.Single(h => h.FieldKey == "service");
        Assert.Equal("high", serviceHint.Confidence);
    }

    [Fact]
    public void ScoreOrgName_service_prefix_inclusion_is_high()
    {
        var score = EmployeeImportFuzzyMatcher.ScoreOrgName(
            "service", "service satisfaction client", "satisfaction client");
        Assert.True(score >= EmployeeImportFuzzyMatcher.HighThreshold);
    }

    [Fact]
    public void GapAnalyzer_reuses_existing_service_when_prefix_differs()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Pôle", "Cellule", "Service"],
            [["a@test.ma", "Ali", "Ben", "pilote", "Pôle A", "Support", "service satisfaction client"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email", [1] = "firstName", [2] = "lastName", [3] = "role",
            [4] = "pole", [5] = "cellule", [6] = "service",
        };

        var roles = new List<PlanningService.Models.Role> { new() { Id = 1, Name = "Pilote" } };
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows =
            [
                new OrgHierarchyRow(42, "Satisfaction client", 1, "Support", 1, "Pôle A")
            ],
            Roles = roles
        };

        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        Assert.Empty(result.PendingOrgCreations);
        Assert.DoesNotContain(result.OrgLineIssues, i => i.Severity == "error");
        var row = Assert.Single(result.ResolvedRows);
        Assert.Equal("Satisfaction client", row.Service);
    }

    [Fact]
    public void OrgFuzzyMatcher_normalizes_pole_nord()
    {
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows =
            [
                new OrgHierarchyRow(1, "Equipe", 1, "Cellule", 1, "Pôle Nord")
            ]
        };

        var resolution = EmployeeImportOrgFuzzyMatcher.ResolveOrgNames(snapshot, "Pole Nord", null, null);
        Assert.Equal("Pôle Nord", resolution.Pole);
        Assert.Contains(resolution.Hints, h => h.Confidence is "high" or "medium");
    }

    [Fact]
    public void MappingValidation_prenom_does_not_match_nom()
    {
        var fields = EmployeeImportFieldRegistry.DefaultFields
            .Select(f => new FieldMatchTarget(f.FieldKey, f.Label, f.Aliases))
            .ToList();
        var matcher = new EmployeeImportColumnMatcher();
        var result = matcher.MatchHeaders(["Prénom"], fields);
        Assert.Equal("firstName", result[0].SuggestedFieldKey);
    }
}

public class EmployeeImportOrgGapAnalyzerTests
{
    [Fact]
    public void GapAnalyzer_proposes_pole_for_chef_de_projet()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Pôle"],
            [["rp@test.ma", "Ali", "Ben", "rp", "Nord"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName",
            [3] = "role",
            [4] = "pole",
        };

        var roles = new List<PlanningService.Models.Role>
        {
            new() { Id = 1, Name = "Chef de projet" },
        };

        var snapshot = new EmployeeImportOrgSnapshot { Rows = [], Roles = roles };
        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        Assert.Single(result.PendingOrgCreations);
        Assert.Equal("pole", result.PendingOrgCreations[0].Type);
        Assert.Equal("Nord", result.PendingOrgCreations[0].Pole);
        Assert.Contains(result.OrgLineIssues, i =>
            i.Message.Contains("Département opérationnel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GapAnalyzer_pole_creation_with_operational_department_has_no_dept_error()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Département", "Pôle"],
            [["rp@test.ma", "Ali", "Ben", "rp", "Commercial", "Nord"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName",
            [3] = "role",
            [4] = "operationalDepartment",
            [5] = "pole",
        };

        var roles = new List<PlanningService.Models.Role>
        {
            new() { Id = 1, Name = "Chef de projet" },
        };

        var snapshot = new EmployeeImportOrgSnapshot { Rows = [], Roles = roles };
        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        Assert.Single(result.PendingOrgCreations);
        Assert.Equal("Commercial", result.PendingOrgCreations[0].OperationalDepartment);
        Assert.DoesNotContain(result.OrgLineIssues, i =>
            i.Message.Contains("Département opérationnel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GapAnalyzer_superviseur_requires_cellule_columns()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Rôle", "Pôle"],
            [["s@test.ma", "superviseur", "Nord"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "role",
            [2] = "pole",
        };

        var roles = new List<PlanningService.Models.Role> { new() { Id = 1, Name = "Superviseur" } };
        var snapshot = new EmployeeImportOrgSnapshot { Rows = [], Roles = roles };
        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        Assert.Contains(result.OrgLineIssues, i => i.Message.Contains("Cellule"));
    }
}

public class EmployeeImportOrgResolverTests
{
    private readonly EmployeeImportOrgResolver _resolver = new(null!);

    [Fact]
    public void ResolveSubServiceId_matches_pole_cellule_service()
    {
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows =
            [
                new OrgHierarchyRow(10, "Equipe A", 5, "Cellule X", 1, "Pôle Nord")
            ]
        };

        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = "Pôle Nord",
            ["cellule"] = "Cellule X",
            ["service"] = "Equipe A"
        };

        var id = _resolver.ResolveSubServiceId(snapshot, mapped);
        Assert.Equal(10, id);
    }

    [Fact]
    public void ResolveSubServiceId_by_service_name_only_when_unique()
    {
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows =
            [
                new OrgHierarchyRow(3, "Support", 1, "C1", 1, "P1")
            ]
        };

        var mapped = new Dictionary<string, string?> { ["service"] = "Support" };
        Assert.Equal(3, _resolver.ResolveSubServiceId(snapshot, mapped));
    }
}
