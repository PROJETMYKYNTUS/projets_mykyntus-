using Planning.Application.DTOs;
using Planning.Infrastructure.Services.EmployeeImport;

namespace Planning.UnitTests;

public class EmployeeImportOrgExistenceTests
{
    private static EmployeeImportOrgSnapshot SnapshotWithPole(string poleName) => new()
    {
        Rows = [new OrgHierarchyRow(1, "Equipe", 1, "Cellule", 1, poleName)],
        Roles = [new Planning.Domain.Entities.Role { Id = 1, Name = "Chef de projet" }]
    };

    [Fact]
    public void PoleExists_matches_normalized_name()
    {
        var snapshot = SnapshotWithPole("Pôle Nord");
        Assert.True(EmployeeImportOrgExistence.PoleExists(snapshot, "Pole Nord"));
        Assert.False(EmployeeImportOrgExistence.PoleExists(snapshot, "pole suivi"));
    }

    [Fact]
    public void AlreadyExists_returns_true_for_existing_pole_creation()
    {
        var snapshot = SnapshotWithPole("pole suivi");
        var item = new PendingOrgCreationDto { Type = "pole", Pole = "pole suivi" };
        Assert.True(EmployeeImportOrgExistence.AlreadyExists(item, snapshot));
    }

    [Fact]
    public void FilterStillNeeded_skips_existing_and_returns_labels()
    {
        var snapshot = SnapshotWithPole("pole suivi");
        var approved = new List<PendingOrgCreationDto>
        {
            new() { Type = "pole", Pole = "pole suivi" },
            new() { Type = "pole", Pole = "nouveau pole" }
        };

        var (needed, skipped) = EmployeeImportOrgExistence.FilterStillNeeded(approved, snapshot);

        Assert.Single(needed);
        Assert.Equal("nouveau pole", needed[0].Pole);
        Assert.Single(skipped);
        Assert.Contains("pole suivi", skipped[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNoDuplicateCreations_throws_on_fuzzy_near_duplicate()
    {
        var snapshot = SnapshotWithPole("Pôle Nord");
        var approved = new List<PendingOrgCreationDto>
        {
            new() { Type = "pole", Pole = "Pole Nordd" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EmployeeImportOrgExistence.ValidateNoDuplicateCreations(approved, snapshot));

        Assert.Contains("très proche existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNoDuplicateCreations_ignores_exact_existing_for_filter()
    {
        var snapshot = SnapshotWithPole("pole suivi");
        var approved = new List<PendingOrgCreationDto>
        {
            new() { Type = "pole", Pole = "pole suivi" }
        };

        var ex = Record.Exception(() =>
            EmployeeImportOrgExistence.ValidateNoDuplicateCreations(approved, snapshot));

        Assert.Null(ex);
    }
}

public class EmployeeImportOrgGapAnalyzerPendingTests
{
    [Fact]
    public void GapAnalyzer_proposes_pole_when_missing()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Pôle"],
            [["rp@test.ma", "Ali", "Ben", "rp", "pole suivi"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName",
            [3] = "role",
            [4] = "pole",
        };

        var roles = new List<Planning.Domain.Entities.Role> { new() { Id = 1, Name = "Chef de projet" } };
        var snapshot = new EmployeeImportOrgSnapshot { Roles = roles };

        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        var pending = Assert.Single(result.PendingOrgCreations);
        Assert.Equal("pole", pending.Type);
        Assert.Equal("pole suivi", pending.Pole);
    }

    [Fact]
    public void GapAnalyzer_does_not_propose_pole_when_exists()
    {
        var analyzer = new EmployeeImportOrgGapAnalyzer(new EmployeeImportOrgResolver(null!));
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Pôle"],
            [["rp@test.ma", "Ali", "Ben", "rp", "Pôle Nord"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName",
            [3] = "role",
            [4] = "pole",
        };

        var roles = new List<Planning.Domain.Entities.Role> { new() { Id = 1, Name = "Chef de projet" } };
        var snapshot = new EmployeeImportOrgSnapshot
        {
            Rows = [new OrgHierarchyRow(1, "Equipe", 1, "Cellule", 1, "Pôle Nord")],
            Roles = roles
        };

        var result = analyzer.AnalyzeFile(parsed, columnMap, snapshot, roles);

        Assert.Empty(result.PendingOrgCreations);
    }
}

public class EmployeeImportStructuralPreconditionsOrgTests
{
    [Fact]
    public void ValidateStructuralPreconditions_does_not_fail_when_pole_missing()
    {
        var parsed = new ParsedImportFile(
            ["Email", "Prénom", "Nom", "Rôle", "Pôle"],
            [["rp@test.ma", "Ali", "Ben", "rp", "pole suivi"]]);

        var columnMap = new Dictionary<int, string>
        {
            [0] = "email",
            [1] = "firstName",
            [2] = "lastName",
            [3] = "role",
            [4] = "pole",
        };

        var request = new EmployeeImportExecuteRequest
        {
            ImportSessionId = Guid.NewGuid(),
            Mappings = [],
            ConfirmOrgProvision = false,
            ApprovedOrgCreations = [],
            AcceptedFuzzyMatches = []
        };

        var snapshot = new EmployeeImportOrgSnapshot
        {
            Roles = [new Planning.Domain.Entities.Role { Id = 1, Name = "Chef de projet" }]
        };

        var ex = Record.Exception(() =>
            EmployeeImportExecutorTestHooks.ValidateStructuralPreconditions(
                parsed, columnMap, request, snapshot));

        Assert.Null(ex);
    }
}
