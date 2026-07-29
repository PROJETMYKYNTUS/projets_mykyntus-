using Kyntus.Iam;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Infrastructure.Persistence;
using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Services;

/// <summary>Mutations org/hiérarchie directement sur PostgreSQL (remplace PrimeInMemoryStore).</summary>
public sealed class PrimeOrgStructureCommandService(PrimeDbContext db, IRebacClient? rebac = null)
{
    public async Task<ChefProjetPoleAssignment> AssignManagerEtageAsync(string userId, string poleId, CancellationToken ct = default)
    {
        var emp = await RequireEmployeeAsync(userId, ct);
        emp.Role = KyntusRoleNames.ChefDeProjet;
        emp.PoleId = poleId.Trim();
        emp.CelluleId = null;
        emp.ServiceId = null;
        emp.ParentId = null;
        await db.SaveChangesAsync(ct);
        return new ChefProjetPoleAssignment { Id = $"m|{emp.Id}|{poleId}", UserId = emp.Id, PoleId = poleId };
    }

    public async Task<SupervisorCelluleAssignment> AssignSupervisorServiceAsync(string userId, string celluleId, CancellationToken ct = default)
    {
        var emp = await RequireEmployeeAsync(userId, ct);
        var cell = await db.Cellules.AsNoTracking().FirstOrDefaultAsync(c => c.Id == celluleId.Trim(), ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        emp.Role = KyntusRoleNames.Superviseur;
        emp.CelluleId = celluleId.Trim();
        emp.PoleId = cell.PoleId;
        emp.ServiceId = null;
        var cp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Role == KyntusRoleNames.ChefDeProjet && e.PoleId == cell.PoleId, ct);
        emp.ParentId = cp?.Id;
        await db.SaveChangesAsync(ct);
        return new SupervisorCelluleAssignment { Id = $"s|{emp.Id}|{celluleId}", UserId = emp.Id, CelluleId = celluleId };
    }

    public async Task<ReferentTechniqueServiceAssignment> AssignCoachSousServiceAsync(string userId, string serviceId, CancellationToken ct = default)
    {
        var emp = await RequireEmployeeAsync(userId, ct);
        var svc = await db.Services.AsNoTracking().Include(s => s.Cellule).FirstOrDefaultAsync(s => s.Id == serviceId.Trim(), ct)
            ?? throw new KeyNotFoundException("Service introuvable.");
        emp.Role = KyntusRoleNames.ReferentTechnique;
        emp.ServiceId = serviceId.Trim();
        emp.CelluleId = svc.CelluleId;
        emp.PoleId = svc.Cellule.PoleId;
        var sup = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Role == KyntusRoleNames.Superviseur && e.CelluleId == svc.CelluleId, ct);
        emp.ParentId = sup?.Id;
        await db.SaveChangesAsync(ct);
        return new ReferentTechniqueServiceAssignment { Id = $"c|{emp.Id}|{serviceId}", UserId = emp.Id, ServiceId = serviceId };
    }

    public async Task<ReferentTechniquePilotLink> AssignCoachPilotAsync(string coachUserId, string pilotUserId, CancellationToken ct = default)
    {
        var pilot = await RequireEmployeeAsync(pilotUserId, ct);
        pilot.Role = KyntusRoleNames.Pilote;
        pilot.ParentId = coachUserId.Trim();
        await db.SaveChangesAsync(ct);
        return new ReferentTechniquePilotLink
        {
            Id = $"p|{coachUserId}|{pilotUserId}",
            ReferentTechniqueUserId = coachUserId.Trim(),
            PilotUserId = pilotUserId.Trim(),
        };
    }

    public async Task RemoveAssignmentByPrefixAsync(string assignmentId, char prefix, CancellationToken ct = default)
    {
        var parts = assignmentId.Split('|');
        if (parts.Length < 3) throw new KeyNotFoundException("Affectation introuvable.");
        var userId = parts[1];
        var nodeId = parts[2].Trim();
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == userId, ct)
            ?? throw new KeyNotFoundException("Employé introuvable.");

        if (prefix == 'm')
            await ReanchorOrDemoteAsync(emp, "ChefDeProjet", nodeId, KyntusRoleNames.ChefDeProjet, ct);
        else if (prefix == 's')
            await ReanchorOrDemoteAsync(emp, "Superviseur", nodeId, KyntusRoleNames.Superviseur, ct);
        else if (prefix == 'c')
            await ReanchorOrDemoteAsync(emp, "ReferentTechnique", nodeId, KyntusRoleNames.ReferentTechnique, ct);
        else if (prefix == 'p' && parts.Length >= 3)
        {
            var pilot = await db.Employees.FirstOrDefaultAsync(e => e.Id == parts[2], ct);
            if (pilot is not null) pilot.ParentId = null;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ReanchorOrDemoteAsync(
        EmployeeEntity emp,
        string kind,
        string removedNodeId,
        string keepRole,
        CancellationToken ct)
    {
        var remaining = await TryGetManagedNodeIdsAsync(emp.Id, kind, ct);
        // Directory a déjà retiré le nœud ; remaining = autres charges.
        if (remaining.Count > 0)
        {
            emp.Role = keepRole;
            var primary = remaining[0];
            switch (kind)
            {
                case "ChefDeProjet":
                    emp.PoleId = primary;
                    emp.CelluleId = null;
                    emp.ServiceId = null;
                    break;
                case "Superviseur":
                {
                    emp.CelluleId = primary;
                    var cell = await db.Cellules.AsNoTracking().FirstOrDefaultAsync(c => c.Id == primary, ct);
                    emp.PoleId = cell?.PoleId ?? emp.PoleId;
                    emp.ServiceId = null;
                    break;
                }
                case "ReferentTechnique":
                {
                    emp.ServiceId = primary;
                    var svc = await db.Services.AsNoTracking().Include(s => s.Cellule)
                        .FirstOrDefaultAsync(s => s.Id == primary, ct);
                    if (svc is not null)
                    {
                        emp.CelluleId = svc.CelluleId;
                        emp.PoleId = svc.Cellule.PoleId;
                    }
                    break;
                }
            }
            return;
        }

        // Plus aucune charge de ce kind : ne démotiver que si l'ancre primaire correspondait au nœud retiré
        // ou qu'aucune ancre n'existe.
        var homeMatches = kind switch
        {
            "ChefDeProjet" => string.Equals(emp.PoleId, removedNodeId, StringComparison.Ordinal),
            "Superviseur" => string.Equals(emp.CelluleId, removedNodeId, StringComparison.Ordinal),
            "ReferentTechnique" => string.Equals(emp.ServiceId, removedNodeId, StringComparison.Ordinal),
            _ => true,
        };
        if (!homeMatches) return;

        if (kind == "ChefDeProjet") emp.PoleId = "";
        if (kind == "Superviseur") emp.CelluleId = null;
        if (kind == "ReferentTechnique") emp.ServiceId = null;
        emp.Role = KyntusRoleNames.Pilote;
    }

    private async Task<IReadOnlyList<string>> TryGetManagedNodeIdsAsync(string employeeId, string kind, CancellationToken ct)
    {
        if (rebac is null || !Guid.TryParse(employeeId, out var guid))
            return [];
        try
        {
            return await rebac.GetManagedNodeIdsAsync(guid, kind, ct);
        }
        catch
        {
            return [];
        }
    }

    public async Task ClearManagerForPoleAsync(string poleId, CancellationToken ct = default)
    {
        var emps = await db.Employees
            .Where(e => e.PoleId == poleId.Trim() && e.Role == KyntusRoleNames.ChefDeProjet)
            .ToListAsync(ct);
        foreach (var e in emps)
        {
            e.PoleId = "";
            e.Role = KyntusRoleNames.Pilote;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearSupervisorForCelluleAsync(string celluleId, CancellationToken ct = default)
    {
        var emps = await db.Employees
            .Where(e => e.CelluleId == celluleId.Trim() && e.Role == KyntusRoleNames.Superviseur)
            .ToListAsync(ct);
        foreach (var e in emps)
        {
            e.CelluleId = null;
            e.Role = KyntusRoleNames.Pilote;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearCoachForServiceAsync(string serviceId, CancellationToken ct = default)
    {
        var emps = await db.Employees
            .Where(e => e.ServiceId == serviceId.Trim() && e.Role == KyntusRoleNames.ReferentTechnique)
            .ToListAsync(ct);
        foreach (var e in emps)
        {
            e.ServiceId = null;
            e.Role = KyntusRoleNames.Pilote;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPilotToServiceAsync(string employeeId, string serviceId, CancellationToken ct = default)
    {
        var emp = await RequireEmployeeAsync(employeeId, ct);
        emp.Role = KyntusRoleNames.Pilote;
        emp.ServiceId = serviceId.Trim();
        var svc = await db.Services.AsNoTracking().Include(s => s.Cellule).FirstAsync(s => s.Id == serviceId.Trim(), ct);
        emp.CelluleId = svc.CelluleId;
        emp.PoleId = svc.Cellule.PoleId;
        var coach = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Role == KyntusRoleNames.ReferentTechnique && e.ServiceId == serviceId.Trim(), ct);
        emp.ParentId = coach?.Id;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemovePilotFromServiceAsync(string employeeId, string serviceId, CancellationToken ct = default)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId.Trim() && e.ServiceId == serviceId.Trim(), ct);
        if (emp is null) throw new KeyNotFoundException("Pilote introuvable.");
        emp.ParentId = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task<EmployeeEntity> RequireEmployeeAsync(string userId, CancellationToken ct) =>
        await db.Employees.FirstOrDefaultAsync(e => e.Id == userId.Trim(), ct)
        ?? throw new KeyNotFoundException("Employé introuvable.");
}

/// <summary>Lecture admin/audit/supervisor depuis PostgreSQL (remplace mocks PrimeInMemoryStore).</summary>
public sealed class PrimeAdminDbReadService(PrimeDbContext db) : IPrimeAdminReadAppService
{
    public AdminDashboardResponse GetAdminDashboard() => new()
    {
        Kpis = new AdminSystemKpi { TotalGeneratedPrimes = db.EmployeePrimeServiceFiches.Count() },
        Charts = new AdminDashboardCharts(),
        Alerts = [],
    };

    public async Task<List<AdminAuditLog>> GetAuditLogsAsync(CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.At)
            .Take(100)
            .Select(a => new AdminAuditLog
            {
                Id = a.Id.ToString(),
                Action = a.Action,
                User = a.UserDisplayName,
                Date = a.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm"),
            })
            .ToListAsync(ct);

    public AdminCalculationConfig GetCalculationConfig() => new();
    public AdminCalculationConfig SaveCalculationConfig(AdminCalculationConfig payload) => payload;
    public List<AdminRbacRow> GetRbacMatrix() => [];
    public List<AdminRbacRow> ToggleRbacPermission(string role, string permission) => [];
    public AdminWorkflowConfig GetWorkflowConfig() => new() { Steps = ["Draft", "Validated"], SlaHours = 72, NotificationsEnabled = true };
    public AdminWorkflowConfig SaveWorkflowConfig(AdminWorkflowConfig payload) => payload;
    public List<AdminAnomaly> GetAdminAnomalies() => [];
    public List<AdminAnomaly> UpdateAnomalyStatus(string id, string status) => [];
    public AuditDashboardResponse GetAuditDashboard() => new();
    public List<AuditOperation> GetOperations() => [];
    public List<AuditTrailLog> GetAuditTrailLogs() => [];
    public List<AuditAnomaly> GetAuditAnomalies() => [];
    public List<SupervisorPrimeRow> GetSupervisorPrimes(string supervisorUserId, string? period) => [];
    public SupervisorDashboardResponse GetSupervisorDashboard(string supervisorUserId) => new();
    public SupervisorPrimeRow ValidateAsSupervisor(string supervisorUserId, string resultId) => new();
    public SupervisorPrimeRow RejectAsSupervisor(string supervisorUserId, string resultId) => new();
    public SupervisorCalculateResponse ComputePrimeSupervisor(SupervisorCalculateRequest req) => new();
    public List<PrimeConfigItem> GetPrimeConfigs(string? kind, string? sector, string? groupCode, string? activityType) => [];
    public PrimeConfigItem CreatePrimeConfig(PrimeConfigUpsertRequest req) => new() { Id = Guid.NewGuid().ToString("N"), Label = req.Label };
    public PrimeConfigItem UpdatePrimeConfig(string id, PrimeConfigUpsertRequest req) => new() { Id = id, Label = req.Label };
    public void DeletePrimeConfig(string id) { }
}
