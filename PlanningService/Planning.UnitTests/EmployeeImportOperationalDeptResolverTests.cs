using Planning.Infrastructure.Services;
using Planning.Infrastructure.Services.EmployeeImport;

namespace Planning.UnitTests;

public class EmployeeImportOperationalDeptResolverTests
{
    [Fact]
    public void Resolve_matches_code_dash_name_format()
    {
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "OP-001 - departement operationnel",
            departments);

        Assert.NotNull(id);
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111110001"), id);
    }

    [Fact]
    public void Resolve_matches_code_only_prefix()
    {
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "OP-001",
            departments);

        Assert.NotNull(id);
    }

    [Fact]
    public void Resolve_tolerates_wrong_code_when_name_matches()
    {
        // Le fichier référence « OP-003 » mais seul OP-001 existe : le nom suffit (inclusion).
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "OP-003 - departement operationnel",
            departments);

        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111110001"), id);
    }

    [Fact]
    public void Resolve_tolerates_name_only_value()
    {
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "departement operationnel",
            departments);

        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111110001"), id);
    }

    [Fact]
    public void Resolve_returns_null_when_tolerant_match_is_ambiguous()
    {
        // Deux départements portent le même nom : un code inexistant ne doit pas être deviné.
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
            new()
            {
                Id = "11111111-1111-4111-8111-111111110002",
                Code = "OP-002",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "OP-009 - departement operationnel",
            departments);

        Assert.Null(id);
    }

    [Fact]
    public void Resolve_returns_null_for_unrelated_value()
    {
        var departments = new List<DirectoryOperationalDepartmentJson>
        {
            new()
            {
                Id = "11111111-1111-4111-8111-111111110001",
                Code = "OP-001",
                Name = "departement operationnel",
                Kind = "Operational",
                IsActive = true,
            },
        };

        var id = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            "service marketing digital",
            departments);

        Assert.Null(id);
    }
}
