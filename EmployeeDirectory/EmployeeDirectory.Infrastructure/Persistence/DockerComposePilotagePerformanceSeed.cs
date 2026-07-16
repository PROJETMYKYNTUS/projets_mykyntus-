using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>
/// Seed idempotent du pôle « pilotage performance » + collaborateurs (Organisation RH).
/// Activé avec KYNTUS_DIRECTORY_DEMO_SEED ou KYNTUS_DEMO_ENRICHMENT.
/// </summary>
internal static class DockerComposePilotagePerformanceSeed
{
    internal static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        DirectoryDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        // Déjà seedé avec nos IDs stables
        if (await db.Employees.AnyAsync(e => e.Id == PilotagePerformanceRoster.Employees[0].Id, ct))
        {
            logger?.LogInformation("Directory pilotage performance : déjà seedé.");
            return;
        }

        // Si un pôle du même nom existe déjà (créé via UI), rattacher les employés à cette structure.
        var existingPole = await db.OrgPoles
            .FirstOrDefaultAsync(p => p.Id == PilotagePerformanceRoster.PoleId
                || EF.Functions.ILike(p.Name, "%pilotage%performance%"), ct);

        var dept = await EnsureDepartmentAsync(db, existingPole?.BusinessDepartmentId, ct);
        var pole = await EnsurePoleAsync(db, dept.Id, existingPole, ct);
        var cellule = await EnsureCelluleAsync(db, pole.Id, ct);
        var service = await EnsureServiceAsync(db, cellule.Id, ct);

        var malak = PilotagePerformanceRoster.Employees[0];
        var salim = PilotagePerformanceRoster.Employees[1];
        var younes = PilotagePerformanceRoster.Employees[2];

        foreach (var spec in PilotagePerformanceRoster.Employees)
        {
            await UpsertEmployeeAsync(db, spec, dept.Id, pole.Id, cellule.Id, service.Id, malak.Id, salim.Id, younes.Id, ct);
        }

        // Persister les employés avant les affectations (évite les échecs d’ordre EF / contraintes).
        await db.SaveChangesAsync(ct);

        await EnsureAssignmentsAsync(db, pole.Id, cellule.Id, service.Id, ct);
        await db.SaveChangesAsync(ct);
        logger?.LogInformation(
            "Directory pilotage performance : pôle « {Pole} », {Count} collaborateurs.",
            pole.Name,
            PilotagePerformanceRoster.Employees.Length);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_DIRECTORY_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"], "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<BusinessDepartment> EnsureDepartmentAsync(
        DirectoryDbContext db,
        Guid? preferredId,
        CancellationToken ct)
    {
        if (preferredId is Guid pid)
        {
            var existing = await db.BusinessDepartments.FirstOrDefaultAsync(d => d.Id == pid, ct);
            if (existing is not null)
                return existing;
        }

        var byCode = await db.BusinessDepartments
            .FirstOrDefaultAsync(d => d.Code == PilotagePerformanceRoster.DepartmentCode, ct);
        if (byCode is not null)
            return byCode;

        var byId = await db.BusinessDepartments
            .FirstOrDefaultAsync(d => d.Id == PilotagePerformanceRoster.DepartmentId, ct);
        if (byId is not null)
            return byId;

        var dept = new BusinessDepartment
        {
            Id = PilotagePerformanceRoster.DepartmentId,
            Code = PilotagePerformanceRoster.DepartmentCode,
            Name = PilotagePerformanceRoster.DepartmentName,
            Kind = BusinessDepartmentKind.Operational,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.BusinessDepartments.Add(dept);
        await db.SaveChangesAsync(ct);
        return dept;
    }

    private static async Task<OrgPole> EnsurePoleAsync(
        DirectoryDbContext db,
        Guid deptId,
        OrgPole? existing,
        CancellationToken ct)
    {
        if (existing is not null)
        {
            if (existing.BusinessDepartmentId is null)
                existing.BusinessDepartmentId = deptId;
            if (string.IsNullOrWhiteSpace(existing.Name))
                existing.Name = PilotagePerformanceRoster.PoleName;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var pole = new OrgPole
        {
            Id = PilotagePerformanceRoster.PoleId,
            Name = PilotagePerformanceRoster.PoleName,
            BusinessDepartmentId = deptId,
        };
        db.OrgPoles.Add(pole);
        await db.SaveChangesAsync(ct);
        return pole;
    }

    private static async Task<OrgCellule> EnsureCelluleAsync(DirectoryDbContext db, string poleId, CancellationToken ct)
    {
        var cell = await db.OrgCellules.FirstOrDefaultAsync(
            c => c.Id == PilotagePerformanceRoster.CelluleId
                || (c.PoleId == poleId && EF.Functions.ILike(c.Name, "%suivi%KPI%")),
            ct);
        if (cell is not null)
        {
            cell.PoleId = poleId;
            cell.Name = PilotagePerformanceRoster.CelluleName;
            await db.SaveChangesAsync(ct);
            return cell;
        }

        cell = new OrgCellule
        {
            Id = PilotagePerformanceRoster.CelluleId,
            Name = PilotagePerformanceRoster.CelluleName,
            PoleId = poleId,
        };
        db.OrgCellules.Add(cell);
        await db.SaveChangesAsync(ct);
        return cell;
    }

    private static async Task<OrgService> EnsureServiceAsync(DirectoryDbContext db, string celluleId, CancellationToken ct)
    {
        var svc = await db.OrgServices.FirstOrDefaultAsync(
            s => s.Id == PilotagePerformanceRoster.ServiceId
                || (s.CelluleId == celluleId && EF.Functions.ILike(s.Name, "%analyse%operationnelle%")),
            ct);
        if (svc is not null)
        {
            svc.CelluleId = celluleId;
            svc.Name = PilotagePerformanceRoster.ServiceName;
            await db.SaveChangesAsync(ct);
            return svc;
        }

        svc = new OrgService
        {
            Id = PilotagePerformanceRoster.ServiceId,
            Name = PilotagePerformanceRoster.ServiceName,
            CelluleId = celluleId,
        };
        db.OrgServices.Add(svc);
        await db.SaveChangesAsync(ct);
        return svc;
    }

    private static async Task UpsertEmployeeAsync(
        DirectoryDbContext db,
        PilotagePerformanceRoster.EmployeeSpec spec,
        Guid deptId,
        string poleId,
        string celluleId,
        string serviceId,
        Guid chefId,
        Guid superviseurId,
        Guid referentId,
        CancellationToken ct)
    {
        var email = spec.Email.Trim().ToLowerInvariant();
        var row = await db.Employees.FirstOrDefaultAsync(
            e => e.Id == spec.Id || e.Email.ToLower() == email,
            ct);

        if (row is null)
        {
            row = new Employee
            {
                Id = spec.Id,
                Email = spec.Email,
                FirstName = spec.FirstName,
                LastName = spec.LastName,
                Role = spec.Role,
                HireDate = DateTime.UtcNow.AddMonths(-8),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                AuthSubjectId = spec.Id,
            };
            db.Employees.Add(row);
        }
        else
        {
            row.FirstName = spec.FirstName;
            row.LastName = spec.LastName;
            row.Email = spec.Email;
            row.Role = spec.Role;
            row.IsActive = true;
            row.AuthSubjectId ??= spec.Id;
            row.UpdatedAt = DateTime.UtcNow;
        }

        row.BusinessDepartmentId = deptId;
        row.PoleId = poleId;
        row.CelluleId = celluleId;
        row.ServiceId = serviceId;
        row.ChefDeProjetId = chefId;
        row.SuperviseurId = superviseurId;
        row.ReferentTechniqueId = referentId;
        row.ParentId = spec.Role switch
        {
            "Pilote" => referentId,
            "Référent technique" => superviseurId,
            "Superviseur" => chefId,
            _ => null,
        };

        await Task.CompletedTask;
    }

    private static async Task EnsureAssignmentsAsync(
        DirectoryDbContext db,
        string poleId,
        string celluleId,
        string serviceId,
        CancellationToken ct)
    {
        foreach (var spec in PilotagePerformanceRoster.Employees)
        {
            if (spec.Assignment is null)
                continue;

            var (nodeId, level) = spec.Node switch
            {
                "pole" => (poleId, OrgNodeLevel.Pole),
                "cellule" => (celluleId, OrgNodeLevel.Cellule),
                _ => (serviceId, OrgNodeLevel.Service),
            };

            var exists = await db.OrgAssignments.AnyAsync(
                a => a.Kind == spec.Assignment
                    && a.NodeId == nodeId
                    && a.EmployeeId == spec.Id
                    && a.EffectiveTo == null,
                ct);
            if (exists)
                continue;

            db.OrgAssignments.Add(new OrgAssignment
            {
                Id = Guid.NewGuid(),
                Kind = spec.Assignment.Value,
                NodeId = nodeId,
                NodeLevel = level,
                EmployeeId = spec.Id,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                ChangeReason = "Seed pilotage performance",
            });
        }
    }
}
