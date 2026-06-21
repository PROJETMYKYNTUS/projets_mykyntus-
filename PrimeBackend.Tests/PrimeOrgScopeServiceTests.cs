using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public class PrimeOrgScopeServiceTests
{
    [Fact]
    public async Task GetOperationalOrgTreeAsync_ReturnsFourLevelHierarchy()
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PrimeDbContext(options);

        db.BusinessDepartments.Add(new BusinessDepartmentEntity
        {
            Id = "dept-1",
            Code = "COM",
            Name = "Commercial",
            Kind = "Operational",
            IsActive = true,
        });
        db.Poles.Add(new PoleEntity
        {
            Id = "pole-1",
            Name = "Pôle A",
            Cellules =
            [
                new CelluleEntity
                {
                    Id = "cell-1",
                    Name = "Cellule 1",
                    PoleId = "pole-1",
                    Services = [new ServiceEntity { Id = "svc-1", Name = "Service 1", CelluleId = "cell-1" }],
                },
            ],
        });
        db.BusinessDepartmentPoles.Add(new BusinessDepartmentPoleEntity
        {
            Id = Guid.NewGuid(),
            BusinessDepartmentId = "dept-1",
            PoleId = "pole-1",
        });
        await db.SaveChangesAsync();

        var org = new PrimeOrgScopeService(db);
        var tree = await org.GetOperationalOrgTreeAsync();

        Assert.Single(tree.OperationalDepartments);
        var dept = tree.OperationalDepartments[0];
        Assert.Equal("Commercial", dept.Name);
        Assert.Single(dept.Poles);
        Assert.Equal("Pôle A", dept.Poles[0].Name);
        Assert.Single(dept.Poles[0].Cellules);
        Assert.Equal("Cellule 1", dept.Poles[0].Cellules[0].Name);
        Assert.Single(dept.Poles[0].Cellules[0].Services);
        Assert.Equal("Service 1", dept.Poles[0].Cellules[0].Services[0].Name);
        Assert.Empty(tree.UnassignedPoles);
    }

    [Fact]
    public async Task GetLegacyEmployeesAsync_IncludesBusinessDepartmentFields()
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PrimeDbContext(options);
        db.Employees.Add(new EmployeeEntity
        {
            Id = "emp-1",
            FirstName = "Salma",
            LastName = "Hajib",
            Role = "Manager",
            Email = "h@example.com",
            PoleId = "",
            BusinessDepartmentId = "dept-1",
            BusinessDepartmentKind = "Operational",
        });
        await db.SaveChangesAsync();

        var org = new PrimeOrgScopeService(db);
        var employees = await org.GetLegacyEmployeesAsync();

        Assert.Single(employees);
        Assert.Equal("dept-1", employees[0].BusinessDepartmentId);
        Assert.Equal("Operational", employees[0].BusinessDepartmentKind);
    }
}
