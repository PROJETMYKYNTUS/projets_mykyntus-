using EmployeeDirectory.Application.Dtos;

namespace EmployeeDirectory.UnitTests;

public class DirectoryStructureAssignmentTests
{
    [Fact]
    public void StructuralRoleAssignmentResult_exposes_added_and_revoked_on_node()
    {
        var result = new StructuralRoleAssignmentResult(
            [new RevokedStructuralRoleDto("Chef de projet", "pole-1", null, null)],
            [new NodeIncumbentRevokedDto("emp-old", "ChefDeProjet", "pole-1")],
            "emp-new");

        Assert.Single(result.Revoked);
        Assert.Single(result.RevokedOnNode);
        Assert.Equal("emp-new", result.AddedEmployeeId);
    }
}
