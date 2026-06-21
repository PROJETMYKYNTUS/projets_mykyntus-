using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public class AllowanceWorkflowTests
{
    [Fact]
    public void NextStatusAfterApproval_Follows_Rh_Compta_Paid_Chain()
    {
        Assert.Equal(
            AllowanceRequestStatuses.RhApproved,
            AllowanceValidationRoles.NextStatusAfterApproval(AllowanceRequestStatuses.ManagerApproved));
        Assert.Equal(
            AllowanceRequestStatuses.ComptaApproved,
            AllowanceValidationRoles.NextStatusAfterApproval(AllowanceRequestStatuses.RhApproved));
        Assert.Equal(
            AllowanceRequestStatuses.Paid,
            AllowanceValidationRoles.NextStatusAfterApproval(AllowanceRequestStatuses.ComptaApproved));
    }

    [Fact]
    public void ExpectedRoleForStatus_NoManagerStep_OnSubmitted()
    {
        Assert.Null(AllowanceValidationRoles.ExpectedRoleForStatus(AllowanceRequestStatuses.Submitted));
        Assert.Equal("RH", AllowanceValidationRoles.ExpectedRoleForStatus(AllowanceRequestStatuses.ManagerApproved));
        Assert.Equal("Comptabilité", AllowanceValidationRoles.ExpectedRoleForStatus(AllowanceRequestStatuses.RhApproved));
    }

    [Fact]
    public void CanActAtStatus_RhAndComptaRoles()
    {
        Assert.True(AllowanceValidationRoles.CanActAtStatus("RH", AllowanceRequestStatuses.ManagerApproved));
        Assert.False(AllowanceValidationRoles.CanActAtStatus("Manager", AllowanceRequestStatuses.ManagerApproved));
        Assert.True(AllowanceValidationRoles.CanActAtStatus("Comptabilité", AllowanceRequestStatuses.RhApproved));
        Assert.True(AllowanceValidationRoles.CanActAtStatus("Comptable", AllowanceRequestStatuses.RhApproved));
        Assert.True(AllowanceValidationRoles.CanActAtStatus("Comptable", AllowanceRequestStatuses.ComptaApproved));
    }

    [Fact]
    public async Task SubmitAsync_TransitionsDraftToManagerApproved()
    {
        var (db, requests, typeId, requestId) = await SeedDraftRequestAsync();
        await using (db)
        {
            var result = await requests.SubmitAsync(requestId, "mgr", CancellationToken.None);
            Assert.Equal(AllowanceRequestStatuses.ManagerApproved, result.Status);
            Assert.NotNull(result.ManagerApprovedAt);
        }
    }

    [Fact]
    public async Task Manager_CannotApproveOwnSubmission()
    {
        var (db, requests, _, requestId) = await SeedDraftRequestAsync();
        await using (db)
        {
            await requests.SubmitAsync(requestId, "mgr", CancellationToken.None);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => requests.ApproveAsync(requestId, "mgr", "Manager", CancellationToken.None));
        }
    }

    [Fact]
    public async Task AllowanceScopeService_DirectReports_OnlySupportHierarchy()
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PrimeDbContext(options);
        db.Employees.AddRange(
            new EmployeeEntity
            {
                Id = "mgr",
                FirstName = "M",
                LastName = "G",
                Role = "Manager",
                Email = "m@test.ma",
                PoleId = "",
                BusinessDepartmentKind = "Support",
            },
            new EmployeeEntity
            {
                Id = "e1",
                FirstName = "A",
                LastName = "B",
                Role = "Pilote",
                Email = "a@test.ma",
                ParentId = "mgr",
                PoleId = "",
                BusinessDepartmentKind = "Support",
            });
        await db.SaveChangesAsync();

        var scope = new AllowanceScopeService(db);
        var ids = await scope.GetDirectReportIdsAsync("mgr", CancellationToken.None);
        Assert.Single(ids);
        Assert.Contains("e1", ids);
    }

    [Fact]
    public void AllowanceMenu_DoesNotReferenceParrainageModule()
    {
        var paths = new[]
        {
            "/allowances",
            "/allowances/requests",
            "/allowances/inbox",
            "/allowances/my",
            "/allowances/catalog",
        };
        Assert.All(paths, p => Assert.DoesNotContain("parrainage", p, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(PrimeDbContext Db, AllowanceRequestService Requests, Guid TypeId, Guid RequestId)> SeedDraftRequestAsync()
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PrimeDbContext(options);
        var deptId = Guid.NewGuid().ToString();
        var typeId = Guid.NewGuid();
        db.BusinessDepartments.Add(new BusinessDepartmentEntity
        {
            Id = deptId,
            Code = "SUP-001",
            Name = "IT",
            Kind = "Support",
            ManagerEmployeeId = "mgr",
            IsActive = true,
        });
        db.Employees.AddRange(
            new EmployeeEntity
            {
                Id = "mgr",
                FirstName = "M",
                LastName = "G",
                Role = "Manager",
                Email = "m@test.ma",
                PoleId = "",
                BusinessDepartmentId = deptId,
                BusinessDepartmentKind = "Support",
            },
            new EmployeeEntity
            {
                Id = "e1",
                FirstName = "A",
                LastName = "B",
                Role = "Pilote",
                Email = "a@test.ma",
                ParentId = "mgr",
                PoleId = "",
                BusinessDepartmentId = deptId,
                BusinessDepartmentKind = "Support",
            });
        db.AllowanceTypes.Add(new AllowanceTypeEntity
        {
            Id = typeId,
            Code = "TEST",
            Label = "Test",
            Category = "Cat",
            CalculationMode = "Manual",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var scope = new AllowanceScopeService(db);
        var audit = new PrimeAuditLogService(db, new HttpContextAccessor());
        var requests = new AllowanceRequestService(db, scope, audit);
        var created = await requests.CreateAsync("mgr", new CreateAllowanceRequestBody
        {
            EmployeeId = "e1",
            AllowanceTypeId = typeId,
            Period = "2026-06",
            Amount = 500m,
            Reason = "Test",
        }, CancellationToken.None);
        return (db, requests, typeId, created.Id);
    }
}
