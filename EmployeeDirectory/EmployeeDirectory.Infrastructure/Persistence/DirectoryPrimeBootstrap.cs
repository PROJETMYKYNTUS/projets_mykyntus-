using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EmployeeDirectory.Domain.Entities;
using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;
using DomainNodeLevel = EmployeeDirectory.Domain.Enums.OrgNodeLevel;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>
/// Aligne Employee Directory avec Prime lorsque le volume Directory est vide
/// (employés Planning/Prime jamais synchronisés via RabbitMQ).
/// </summary>
public static class DirectoryPrimeBootstrap
{
    public static async Task BootstrapFromPrimeIfNeededAsync(
        IServiceProvider services,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DirectoryPrimeBootstrap");

        var employeeCount = await db.Employees.CountAsync(ct);
        var poleCount = await db.OrgPoles.CountAsync(ct);
        if (employeeCount > 0 && poleCount > 0 && !await HasUnmappedOrgReferencesAsync(db, ct))
            return;

        var baseUrl = (config["Prime:BaseUrl"] ?? "http://prime-backend:8080/").TrimEnd('/') + "/";
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(45) };

        const int maxAttempts = 30;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await RunBootstrapAsync(db, http, employeeCount, poleCount, log, ct);
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                log.LogWarning(ex, "Directory Prime bootstrap attempt {Attempt}/{Max} — retry in 3s.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                employeeCount = await db.Employees.CountAsync(ct);
                poleCount = await db.OrgPoles.CountAsync(ct);
                if (employeeCount > 0 && poleCount > 0 && !await HasUnmappedOrgReferencesAsync(db, ct)) return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Directory Prime bootstrap abandoned after {Max} attempts.", maxAttempts);
                return;
            }
        }
    }

    private static async Task RunBootstrapAsync(
        DirectoryDbContext db,
        HttpClient http,
        int employeeCount,
        int poleCount,
        ILogger log,
        CancellationToken ct)
    {
        var departments = await http.GetFromJsonAsync<List<PrimeDepartmentJson>>("api/prime/departments", ct)
            ?? [];
        foreach (var dept in departments)
        {
            await UpsertPoleAsync(db, dept.Id, dept.Name, ct);
            foreach (var cellule in dept.Poles ?? [])
            {
                await UpsertCelluleAsync(db, cellule.Id, cellule.Name, dept.Id, ct);
                foreach (var service in cellule.Cells ?? [])
                {
                    await UpsertServiceAsync(db, service.Id, service.Name, cellule.Id, ct);
                }
            }
        }
        if (departments.Count > 0)
            log.LogInformation("Directory org bootstrap: {Count} pôle(s) depuis Prime.", departments.Count);

        if (employeeCount == 0)
        {
            var employees = await http.GetFromJsonAsync<List<PrimeEmployeeJson>>("api/prime/employees", ct)
                ?? [];
            foreach (var e in employees)
            {
                await UpsertEmployeeAsync(db, e, ct);
            }

            await ImportAssignmentsAsync(http, db, ct);
            log.LogInformation("Directory employee bootstrap: {Count} employé(s) depuis Prime.", employees.Count);
        }
    }

    private static async Task<bool> HasUnmappedOrgReferencesAsync(DirectoryDbContext db, CancellationToken ct)
    {
        var poleIds = await db.OrgPoles.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var celluleIds = await db.OrgCellules.AsNoTracking().Select(c => c.Id).ToListAsync(ct);
        var serviceIds = await db.OrgServices.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var poleSet = poleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var celluleSet = celluleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var serviceSet = serviceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var employees = await db.Employees.AsNoTracking()
            .Select(e => new { e.PoleId, e.CelluleId, e.ServiceId })
            .ToListAsync(ct);

        foreach (var e in employees)
        {
            if (!string.IsNullOrWhiteSpace(e.PoleId) && !poleSet.Contains(e.PoleId)) return true;
            if (!string.IsNullOrWhiteSpace(e.CelluleId) && !celluleSet.Contains(e.CelluleId)) return true;
            if (!string.IsNullOrWhiteSpace(e.ServiceId) && !serviceSet.Contains(e.ServiceId)) return true;
        }

        return false;
    }

    private static async Task ImportAssignmentsAsync(HttpClient http, DirectoryDbContext db, CancellationToken ct)
    {
        var managers = await http.GetFromJsonAsync<List<PrimeAssignmentJson>>("api/prime/org/assignments/manager-etage", ct)
            ?? [];
        foreach (var row in managers)
        {
            if (!Guid.TryParse(row.UserId, out var employeeId)) continue;
            var nodeId = (row.EtageId ?? "").Trim();
            if (nodeId.Length == 0) continue;
            await UpsertAssignmentAsync(db, DomainAssignmentKind.ChefDeProjet, nodeId, DomainNodeLevel.Pole, employeeId, ct);
        }

        var supervisors = await http.GetFromJsonAsync<List<PrimeSupervisorAssignmentJson>>(
            "api/prime/org/assignments/supervisor-service", ct) ?? [];
        foreach (var row in supervisors)
        {
            if (!Guid.TryParse(row.UserId, out var employeeId)) continue;
            var nodeId = (row.CelluleId ?? row.ServiceId ?? "").Trim();
            if (nodeId.Length == 0) continue;
            await UpsertAssignmentAsync(db, DomainAssignmentKind.Superviseur, nodeId, DomainNodeLevel.Cellule, employeeId, ct);
        }

        var coaches = await http.GetFromJsonAsync<List<PrimeCoachAssignmentJson>>(
            "api/prime/org/assignments/coach-sous-service", ct) ?? [];
        foreach (var row in coaches)
        {
            if (!Guid.TryParse(row.UserId, out var employeeId)) continue;
            var nodeId = (row.ServiceId ?? row.SousServiceId ?? "").Trim();
            if (nodeId.Length == 0) continue;
            await UpsertAssignmentAsync(db, DomainAssignmentKind.ReferentTechnique, nodeId, DomainNodeLevel.Service, employeeId, ct);
        }
    }

    private static async Task UpsertAssignmentAsync(
        DirectoryDbContext db,
        DomainAssignmentKind kind,
        string nodeId,
        DomainNodeLevel level,
        Guid employeeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        var active = await db.OrgAssignments
            .Where(a => a.Kind == kind && a.NodeId == nodeId && a.EffectiveTo == null)
            .ToListAsync(ct);
        if (active.Any(a => a.EmployeeId == employeeId)) return;

        foreach (var row in active)
            row.EffectiveTo = DateTime.UtcNow;

        db.OrgAssignments.Add(new OrgAssignment
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            NodeId = nodeId,
            NodeLevel = level,
            EmployeeId = employeeId,
            EffectiveFrom = DateTime.UtcNow,
            ChangeReason = "bootstrap-from-prime",
        });

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return;

        switch (kind)
        {
            case DomainAssignmentKind.ChefDeProjet:
                employee.PoleId = nodeId;
                employee.Role = KyntusRoleNames.ChefDeProjet;
                break;
            case DomainAssignmentKind.Superviseur:
                employee.CelluleId = nodeId;
                employee.Role = KyntusRoleNames.Superviseur;
                break;
            case DomainAssignmentKind.ReferentTechnique:
                employee.ServiceId = nodeId;
                employee.Role = KyntusRoleNames.ReferentTechnique;
                break;
        }
        employee.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task UpsertPoleAsync(DirectoryDbContext db, string id, string name, CancellationToken ct)
    {
        var row = await db.OrgPoles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row is null)
            db.OrgPoles.Add(new OrgPole { Id = id, Name = name });
        else
            row.Name = name;
    }

    private static async Task UpsertCelluleAsync(DirectoryDbContext db, string id, string name, string poleId, CancellationToken ct)
    {
        var row = await db.OrgCellules.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row is null)
            db.OrgCellules.Add(new OrgCellule { Id = id, Name = name, PoleId = poleId });
        else
        {
            row.Name = name;
            row.PoleId = poleId;
        }
    }

    private static async Task UpsertServiceAsync(DirectoryDbContext db, string id, string name, string celluleId, CancellationToken ct)
    {
        var row = await db.OrgServices.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null)
            db.OrgServices.Add(new OrgService { Id = id, Name = name, CelluleId = celluleId });
        else
        {
            row.Name = name;
            row.CelluleId = celluleId;
        }
    }

    private static async Task UpsertEmployeeAsync(DirectoryDbContext db, PrimeEmployeeJson e, CancellationToken ct)
    {
        if (!Guid.TryParse(e.Id, out var id)) return;

        var role = KyntusRoleNames.NormalizePlanningRole(e.Role);
        var existing = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            db.Employees.Add(new Employee
            {
                Id = id,
                Email = (e.Email ?? $"{id:N}@kyntus.local").Trim(),
                FirstName = e.FirstName?.Trim() ?? "",
                LastName = e.LastName?.Trim() ?? "",
                Role = role,
                PoleId = string.IsNullOrWhiteSpace(e.PoleId) ? null : e.PoleId,
                CelluleId = string.IsNullOrWhiteSpace(e.CelluleId) ? null : e.CelluleId,
                ServiceId = string.IsNullOrWhiteSpace(e.ServiceId) ? null : e.ServiceId,
                ParentId = Guid.TryParse(e.ParentId, out var parentId) ? parentId : null,
                IsActive = true,
                HireDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            return;
        }

        existing.FirstName = e.FirstName?.Trim() ?? existing.FirstName;
        existing.LastName = e.LastName?.Trim() ?? existing.LastName;
        existing.Email = (e.Email ?? existing.Email).Trim();
        existing.Role = role;
        existing.PoleId = string.IsNullOrWhiteSpace(e.PoleId) ? existing.PoleId : e.PoleId;
        existing.CelluleId = string.IsNullOrWhiteSpace(e.CelluleId) ? existing.CelluleId : e.CelluleId;
        existing.ServiceId = string.IsNullOrWhiteSpace(e.ServiceId) ? existing.ServiceId : e.ServiceId;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private sealed class PrimeDepartmentJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<PrimeCelluleJson>? Poles { get; set; }
    }

    private sealed class PrimeCelluleJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        [JsonPropertyName("cells")]
        public List<PrimeLeafServiceJson>? Cells { get; set; }
    }

    private sealed class PrimeLeafServiceJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class PrimeEmployeeJson
    {
        public string Id { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "";
        public string? ParentId { get; set; }
        public string? PoleId { get; set; }
        public string? CelluleId { get; set; }
        public string? ServiceId { get; set; }
        public string? Email { get; set; }
    }

    private sealed class PrimeAssignmentJson
    {
        public string UserId { get; set; } = "";
        [JsonPropertyName("etageId")]
        public string? EtageId { get; set; }
    }

    private sealed class PrimeSupervisorAssignmentJson
    {
        public string UserId { get; set; } = "";
        public string? CelluleId { get; set; }
        public string? ServiceId { get; set; }
    }

    private sealed class PrimeCoachAssignmentJson
    {
        public string UserId { get; set; } = "";
        public string? ServiceId { get; set; }
        public string? SousServiceId { get; set; }
    }
}
