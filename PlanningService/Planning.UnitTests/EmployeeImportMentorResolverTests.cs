using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services.EmployeeImport;

namespace Planning.UnitTests;

public class EmployeeImportMentorResolverTests
{
    private static readonly Guid ChefGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SupGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RefGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string PoleId = "pole-1";
    private const string CelluleId = "cell-1";
    private const string ServiceId = "svc-1";
    private static readonly Guid WrongSupGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string OtherCelluleId = "cell-2";

    private static EmployeeImportOrgOverview BuildOverview() => new()
    {
        Etages = [new ImportOrgEtageDto { Id = PoleId, Name = "Pôle Alpha" }],
        Services = [new ImportOrgServiceNodeDto { Id = CelluleId, Name = "Cellule Beta", EtageId = PoleId }],
        SousServices = [new ImportOrgSousServiceDto { Id = ServiceId, Name = "Service Gamma", ServiceId = CelluleId }],
        Employees =
        [
            new ImportOrgEmployeeDto
            {
                Id = ChefGuid.ToString(),
                FirstName = "Karim",
                LastName = "Bennani",
                Role = "Chef de projet",
            },
            new ImportOrgEmployeeDto
            {
                Id = SupGuid.ToString(),
                FirstName = "Sara",
                LastName = "Idrissi",
                Role = "Superviseur",
                ParentId = ChefGuid.ToString(),
            },
            new ImportOrgEmployeeDto
            {
                Id = RefGuid.ToString(),
                FirstName = "Youssef",
                LastName = "Alaoui",
                Role = "Référent technique",
                ParentId = SupGuid.ToString(),
            },
        ],
        ManagerEtage = [new ImportOrgManagerEtageDto { UserId = ChefGuid.ToString(), EtageId = PoleId }],
        SupervisorService =
        [
            new ImportOrgSupervisorServiceDto
            {
                UserId = SupGuid.ToString(),
                CelluleId = CelluleId,
                ServiceId = CelluleId,
            },
        ],
        CoachSousService =
        [
            new ImportOrgCoachSousServiceDto
            {
                UserId = RefGuid.ToString(),
                ServiceId = ServiceId,
                SousServiceId = ServiceId,
            },
        ],
    };

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public void ResolveOrgNodeIds_maps_pole_cellule_service_names()
    {
        var overview = BuildOverview();
        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = "Pôle Alpha",
            ["cellule"] = "Cellule Beta",
            ["service"] = "Service Gamma",
        };

        var (poleId, celluleId, serviceId) = EmployeeImportMentorResolver.ResolveOrgNodeIds(overview, mapped);

        Assert.Equal(PoleId, poleId);
        Assert.Equal(CelluleId, celluleId);
        Assert.Equal(ServiceId, serviceId);
    }

    [Fact]
    public async Task ResolveAndValidate_throws_when_superviseur_without_chef()
    {
        await using var db = await CreateDbAsync();
        var overview = BuildOverview();
        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = "Pôle Alpha",
            ["cellule"] = "Cellule Beta",
            ["service"] = "Service Gamma",
            ["superviseurName"] = "Sara Idrissi",
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmployeeImportMentorResolver.ResolveAndValidateAsync(
                db, overview, mapped, "Pilote", CancellationToken.None));

        Assert.Contains("chef de projet", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAndValidate_rejects_superviseur_outside_cellule()
    {
        await using var db = await CreateDbAsync();
        var overview = BuildOverview();
        overview.Employees.Add(new ImportOrgEmployeeDto
        {
            Id = WrongSupGuid.ToString(),
            FirstName = "Ahmed",
            LastName = "Test",
            Role = "Superviseur",
            ParentId = Guid.NewGuid().ToString(),
        });
        overview.SupervisorService.Add(new ImportOrgSupervisorServiceDto
        {
            UserId = WrongSupGuid.ToString(),
            CelluleId = OtherCelluleId,
            ServiceId = OtherCelluleId,
        });

        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = "Pôle Alpha",
            ["cellule"] = "Cellule Beta",
            ["service"] = "Service Gamma",
            ["chefDeProjetName"] = "Karim Bennani",
            ["superviseurName"] = "Ahmed Test",
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmployeeImportMentorResolver.ResolveAndValidateAsync(
                db, overview, mapped, "Pilote", CancellationToken.None));

        Assert.Contains("superviseur", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAndValidate_accepts_valid_mentor_chain()
    {
        await using var db = await CreateDbAsync();
        var overview = BuildOverview();
        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = "Pôle Alpha",
            ["cellule"] = "Cellule Beta",
            ["service"] = "Service Gamma",
            ["chefDeProjetName"] = "Karim Bennani",
            ["superviseurName"] = "Sara Idrissi",
            ["referentTechniqueName"] = "Youssef Alaoui",
        };

        var (chef, sup, referent) = await EmployeeImportMentorResolver.ResolveAndValidateAsync(
            db, overview, mapped, "Pilote", CancellationToken.None);

        Assert.Equal(ChefGuid, chef);
        Assert.Equal(SupGuid, sup);
        Assert.Equal(RefGuid, referent);
    }
}
