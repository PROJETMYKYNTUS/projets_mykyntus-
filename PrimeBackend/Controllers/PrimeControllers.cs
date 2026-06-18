namespace PrimeBackend.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Models;
using PrimeBackend.Dto;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

[ApiController]
[Route("api/prime")]
public class PrimeController(PrimeDbContext? db, PrimeOrgScopeService org) : ControllerBase
{
    /// <summary>Diagnostic : vérifie que l’API écoute et que PostgreSQL répond (utile si 502 via gateway).</summary>
    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        if (db is null)
            return Ok(new { status = "ok", mode = "memory-only" });
        try
        {
            var ok = await db.Database.CanConnectAsync(ct);
            return ok
                ? Ok(new { status = "ok", database = "prime_db" })
                : StatusCode(503, new { status = "db-unreachable" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "db-error", error = ex.Message });
        }
    }

    /// <summary>Lecture référentiel org (bootstrap Directory, SPA authentifiée).</summary>
    [AllowAnonymous]
    [HttpGet("departments")]
    public async Task<ActionResult<List<Department>>> GetPoles(CancellationToken ct) =>
        db == null
            ? StatusCode(503, new { error = "Base de données non configurée." })
            : Ok(await org.GetLegacyDepartmentTreeAsync(ct));

    [AllowAnonymous]
    [HttpGet("employees")]
    public async Task<ActionResult<List<Employee>>> GetEmployees(CancellationToken ct) =>
        db == null
            ? StatusCode(503, new { error = "Base de données non configurée." })
            : Ok(await org.GetLegacyEmployeesAsync(ct));

    [HttpGet("types")]
    public ActionResult<List<PrimeType>> GetPrimeTypes() => Ok(new List<PrimeType>());

    [HttpGet("rules")]
    public ActionResult<List<PrimeRule>> GetPrimeRules() => Ok(new List<PrimeRule>());

    [HttpGet("results")]
    public async Task<ActionResult<List<PrimeResult>>> GetPrimeResults(CancellationToken ct) =>
        db == null
            ? StatusCode(503, new { error = "Base de données non configurée." })
            : Ok(await org.GetPrimeResultsFromFichesAsync(500, ct));

    [HttpGet("my-results")]
    public async Task<ActionResult<List<PrimeResult>>> GetMyPrimeResults([FromQuery] string employeeId, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var list = await org.GetPrimeResultsFromFichesAsync(500, ct);
        return Ok(list.Where(r => r.EmployeeId == employeeId.Trim()).ToList());
    }

    [HttpPut("results/{id}/status")]
    public ActionResult<PrimeResult> UpdatePrimeResultStatus(string id, [FromBody] UpdatePrimeResultStatusRequest req)
        => StatusCode(StatusCodes.Status410Gone,
            new { error = "Utilisez l’API /api/prime/validation pour approuver ou rejeter une fiche." });

    [HttpGet("dashboard-stats")]
    public async Task<ActionResult<object>> GetDashboardStats(CancellationToken ct) =>
        db == null
            ? StatusCode(503, new { error = "Base de données non configurée." })
            : Ok(await org.BuildDashboardStatsAsync(ct));
}

[ApiController]
[Route("api/rp")]
public class RpPrimeController(PrimeRpQueryService rpQueries) : ControllerBase
{
    [HttpGet("assigned-project-ids")]
    public async Task<ActionResult<List<string>>> GetAssignedProjectIds([FromQuery] string rpUserId, CancellationToken ct) =>
        Ok(await rpQueries.GetAssignedProjectIdsAsync(rpUserId, ct));

    [HttpGet("dashboard-stats")]
    public async Task<ActionResult<ChefProjetDashboardStats>> GetChefProjetDashboardStats([FromQuery] string rpUserId, CancellationToken ct) =>
        Ok(await rpQueries.GetDashboardStatsAsync(rpUserId, ct));

    [HttpGet("team-performance")]
    public async Task<ActionResult<List<ChefProjetTeamMemberPerformance>>> GetTeamPerformanceByProject([FromQuery] string rpUserId, CancellationToken ct) =>
        Ok(await rpQueries.GetTeamPerformanceByProjectAsync(rpUserId, ct));

    [HttpGet("manager-validated")]
    public async Task<ActionResult<List<ChefProjetValidationItem>>> GetSuperviseurValidatedPrimes([FromQuery] string rpUserId, CancellationToken ct) =>
        Ok(await rpQueries.GetSuperviseurValidatedPrimesAsync(rpUserId, ct));

    [HttpPut("validations/{id}/status")]
    public async Task<ActionResult<ChefProjetValidationItem>> UpdateRpValidationStatus(
        string id,
        [FromBody] UpdateChefProjetValidationStatusRequest req,
        [FromQuery] string? rpUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rpUserId))
            return BadRequest(new { error = "rpUserId requis." });
        try
        {
            var updated = await rpQueries.UpdateValidationStatusAsync(id, req.Status, rpUserId, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/admin")]
public class AdminPrimeController(PrimeAdminDbReadService admin) : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<AdminDashboardResponse> GetDashboard() => admin.GetAdminDashboard();

    [HttpGet("calculation-config")]
    public ActionResult<AdminCalculationConfig> GetCalculationConfig() => admin.GetCalculationConfig();

    [HttpPut("calculation-config")]
    public ActionResult<AdminCalculationConfig> SaveCalculationConfig([FromBody] AdminCalculationConfig payload) =>
        admin.SaveCalculationConfig(payload);

    [HttpGet("rbac-matrix")]
    public ActionResult<List<AdminRbacRow>> GetRbacMatrix() => admin.GetRbacMatrix();

    [HttpPut("rbac-matrix/toggle")]
    public ActionResult<List<AdminRbacRow>> ToggleRbacPermission([FromBody] ToggleRbacPermissionRequest req) =>
        admin.ToggleRbacPermission(req.Role, req.Permission);

    [HttpGet("workflow-config")]
    public ActionResult<AdminWorkflowConfig> GetWorkflowConfig() => admin.GetWorkflowConfig();

    [HttpPut("workflow-config")]
    public ActionResult<AdminWorkflowConfig> SaveWorkflowConfig([FromBody] AdminWorkflowConfig payload) =>
        admin.SaveWorkflowConfig(payload);

    [HttpGet("audit-logs")]
    public async Task<ActionResult<List<AdminAuditLog>>> GetAuditLogs(CancellationToken ct) =>
        Ok(await admin.GetAuditLogsAsync(ct));

    [HttpGet("anomalies")]
    public ActionResult<List<AdminAnomaly>> GetAnomalies() => admin.GetAdminAnomalies();

    [HttpPut("anomalies/{id}/status")]
    public ActionResult<List<AdminAnomaly>> UpdateAnomalyStatus(string id, [FromBody] UpdateAnomalyStatusRequest req) =>
        admin.UpdateAnomalyStatus(id, req.Status);
}

[ApiController]
[Route("api/audit")]
public class AuditPrimeController(PrimeAdminDbReadService admin) : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<AuditDashboardResponse> GetDashboard() => admin.GetAuditDashboard();

    [HttpGet("operations")]
    public ActionResult<List<AuditOperation>> GetOperations() => admin.GetOperations();

    [HttpGet("trail-logs")]
    public ActionResult<List<AuditTrailLog>> GetAuditTrailLogs() => admin.GetAuditTrailLogs();

    [HttpGet("anomalies")]
    public ActionResult<List<AuditAnomaly>> GetAnomalies() => admin.GetAuditAnomalies();
}

public sealed class EnsureEmployeeFromPlanningRequest
{
    public Guid EmployeeId { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
    public string? PrimeServiceId { get; init; }
}

[ApiController]
[Route("api/prime/org")]
public class PrimeOrgAssignmentsController(
    PrimeOrgStructureCommandService orgCommands,
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    IConfiguration configuration,
    IOrgStructureEventPublisher orgEvents,
    IEmployeeDirectorySyncService employeeDirectorySync) : ControllerBase
{
    private static string NewPersistedOrgId(string prefix)
    {
        var s = Guid.NewGuid().ToString("N");
        return $"{prefix}-{s[..Math.Min(12, s.Length)]}";
    }

    private async Task ExecuteOrgStructureMutationAsync(
        CancellationToken ct,
        Func<Task> mutationAsync,
        Func<Task>? afterMutationAsync = null)
    {
        if (db is null)
        {
            await mutationAsync();
            if (afterMutationAsync is not null)
                await afterMutationAsync();
            return;
        }

        await mutationAsync();
        if (afterMutationAsync is not null)
            await afterMutationAsync();
        if (db is not null)
            await db.SaveChangesAsync(ct);
    }

    private async Task PublishStructureAssignmentAsync(
        OrgAssignmentKind kind,
        string nodeId,
        OrgNodeLevel nodeLevel,
        string employeeId,
        bool removed,
        CancellationToken ct)
    {
        string? email = null;
        string? newRole = null;
        if (!removed && !string.IsNullOrWhiteSpace(employeeId) && db is not null)
        {
            var emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId.Trim(), ct);
            email = emp?.Email;
            newRole = emp?.Role;
        }

        await orgEvents.PublishAssignmentChangedAsync(new OrgAssignmentChangedMessage
        {
            Kind = kind,
            NodeId = nodeId,
            NodeLevel = nodeLevel,
            EmployeeId = employeeId.Trim(),
            EmployeeEmail = email,
            NewRole = newRole,
            Removed = removed
        }, ct);
    }

    /// <summary>Garantit la présence d'un employé Prime avec Id = guid Planning (liaison synchrone Gestion employés).</summary>
    [HttpPost("employees/ensure-from-planning")]
    public async Task<ActionResult<object>> EnsureEmployeeFromPlanning(
        [FromBody] EnsureEmployeeFromPlanningRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (body.EmployeeId == Guid.Empty)
            return BadRequest(new { error = "employeeId est requis." });
        if (string.IsNullOrWhiteSpace(body.Email))
            return BadRequest(new { error = "email est requis." });

        var employeeId = await employeeDirectorySync.EnsureFromPlanningAsync(
            new EmployeeDirectoryUpsertRequest(
                EmployeeId: body.EmployeeId,
                FirstName: body.FirstName.Trim(),
                LastName: body.LastName.Trim(),
                Email: body.Email.Trim(),
                Role: body.Role,
                PrimeServiceId: body.PrimeServiceId),
            ct);

        return Ok(new { employeeId });
    }

    /// <summary>Fusionne les employés en doublon (même email) — conserve le guid Planning si présent.</summary>
    [HttpPost("employees/dedupe-by-email")]
    public async Task<ActionResult<object>> DedupeEmployeesByEmail(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var merged = await employeeDirectorySync.DedupeByEmailAsync(ct);
        return Ok(new { merged });
    }

    [HttpGet("etages")]
    public async Task<ActionResult<List<PoleNode>>> GetEtages(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await db.Poles.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new PoleNode { Id = p.Id, Name = p.Name })
            .ToListAsync(ct));
    }

    [HttpGet("services")]
    public async Task<ActionResult<List<CelluleNode>>> GetServices(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await db.Cellules.AsNoTracking()
            .OrderBy(c => c.PoleId).ThenBy(c => c.Id)
            .Select(c => new CelluleNode { Id = c.Id, Name = c.Name, PoleId = c.PoleId })
            .ToListAsync(ct));
    }

    /// <summary>Cellules supervisées et services (structure RH) pour les écrans indicateurs / filtres.</summary>
    [HttpGet("supervisor-scope")]
    public async Task<ActionResult<List<SupervisorOrgScopePoleDto>>> GetSupervisorScope(
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });
        return Ok(await org.GetSupervisorOrganizationalScopeAsync(supervisorUserId, ct));
    }

    [HttpGet("sous-services")]
    public async Task<ActionResult<List<CelluleNode>>> GetSousServices(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await db.Services.AsNoTracking()
            .OrderBy(s => s.CelluleId).ThenBy(s => s.Id)
            .Select(s => new CelluleNode { Id = s.Id, Name = s.Name, ServiceId = s.CelluleId })
            .ToListAsync(ct));
    }

    [AllowAnonymous]
    [HttpGet("assignments/manager-etage")]
    public async Task<ActionResult<List<ChefProjetPoleAssignment>>> GetChefProjetPoleAssignments(
        [FromQuery] string? userId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Chef de projet");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        var list = rows
            .Where(e => !string.IsNullOrWhiteSpace(e.PoleId))
            .Select(e => new ChefProjetPoleAssignment
            {
                Id = $"m|{e.Id}|{e.PoleId}",
                UserId = e.Id,
                PoleId = e.PoleId,
            })
            .ToList();
        return Ok(list);
    }

    [AllowAnonymous]
    [HttpGet("assignments/supervisor-service")]
    public async Task<ActionResult<List<SupervisorCelluleAssignment>>> GetSupervisorCelluleAssignments(
        [FromQuery] string? userId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Superviseur");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        var list = rows
            .Where(e => !string.IsNullOrWhiteSpace(e.CelluleId))
            .Select(e => new SupervisorCelluleAssignment
            {
                Id = $"s|{e.Id}|{e.CelluleId}",
                UserId = e.Id,
                CelluleId = e.CelluleId,
            })
            .ToList();
        return Ok(list);
    }

    [AllowAnonymous]
    [HttpGet("assignments/coach-sous-service")]
    public async Task<ActionResult<List<ReferentTechniqueServiceAssignment>>> GetReferentTechniqueServiceAssignments(
        [FromQuery] string? userId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Référent technique");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        var list = rows
            .Where(e => !string.IsNullOrWhiteSpace(e.ServiceId))
            .Select(e => new ReferentTechniqueServiceAssignment
            {
                Id = $"c|{e.Id}|{e.ServiceId}",
                UserId = e.Id,
                ServiceId = e.ServiceId,
            })
            .ToList();
        return Ok(list);
    }

    [HttpGet("assignments/coach-pilot")]
    public async Task<ActionResult<List<ReferentTechniquePilotLink>>> GetReferentTechniquePilotLinks(
        [FromQuery] string? coachUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Pilote" && e.ParentId != null);
        if (!string.IsNullOrWhiteSpace(coachUserId)) q = q.Where(e => e.ParentId == coachUserId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        var list = rows
            .Select(e => new ReferentTechniquePilotLink
            {
                Id = $"p|{e.ParentId}|{e.Id}",
                ReferentTechniqueUserId = e.ParentId!,
                PilotUserId = e.Id,
            })
            .ToList();
        return Ok(list);
    }

    [HttpPost("assignments/manager-etage")]
    public async Task<ActionResult<ChefProjetPoleAssignment>> AssignManagerEtage(
        [FromBody] AssignChefProjetPoleRequest req,
        CancellationToken ct)
    {
        try
        {
            return Ok(await orgCommands.AssignManagerEtageAsync(req.UserId, req.PoleId, ct));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/supervisor-service")]
    public async Task<ActionResult<SupervisorCelluleAssignment>> AssignSupervisorService(
        [FromBody] AssignSupervisorCelluleRequest req,
        CancellationToken ct)
    {
        try
        {
            return Ok(await orgCommands.AssignSupervisorServiceAsync(req.UserId, req.ServiceId, ct));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/coach-sous-service")]
    public async Task<ActionResult<ReferentTechniqueServiceAssignment>> AssignCoachSousService(
        [FromBody] AssignReferentTechniqueServiceRequest req,
        CancellationToken ct)
    {
        try
        {
            ReferentTechniqueServiceAssignment? created = null;
            await ExecuteOrgStructureMutationAsync(ct, async () =>
                created = await orgCommands.AssignCoachSousServiceAsync(req.UserId, req.ServiceId, ct));
            return Ok(created!);
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/coach-pilot")]
    public async Task<ActionResult<ReferentTechniquePilotLink>> AssignCoachPilot(
        [FromBody] AssignReferentTechniquePilotRequest req,
        CancellationToken ct)
    {
        try
        {
            ReferentTechniquePilotLink? created = null;
            await ExecuteOrgStructureMutationAsync(ct, async () =>
                created = await orgCommands.AssignCoachPilotAsync(req.ReferentTechniqueUserId, req.PilotUserId, ct));
            return Ok(created!);
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/manager-etage/{assignmentId}")]
    public async Task<IActionResult> RemoveChefProjetPoleAssignment(string assignmentId, CancellationToken ct)
    {
        try
        {
            await orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 'm', ct);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/supervisor-service/{assignmentId}")]
    public async Task<IActionResult> RemoveSupervisorCelluleAssignment(string assignmentId, CancellationToken ct)
    {
        try
        {
            await orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 's', ct);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/coach-sous-service/{assignmentId}")]
    public async Task<IActionResult> RemoveReferentTechniqueServiceAssignment(string assignmentId, CancellationToken ct)
    {
        try
        {
            await orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 'c', ct);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/coach-pilot/{linkId}")]
    public async Task<IActionResult> RemoveReferentTechniquePilotLink(string linkId, CancellationToken ct)
    {
        try
        {
            await orgCommands.RemoveAssignmentByPrefixAsync(linkId, 'p', ct);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("structure/departments")]
    public async Task<ActionResult<Department>> CreateDepartment([FromBody] CreateOrgPoleBody body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Le nom du pôle est requis." });
        var name = body.Name.Trim();
        if (db is not null)
        {
            var poleNames = await db.Poles.AsNoTracking().Select(p => p.Name).ToListAsync(ct);
            try
            {
                OrgStructureRules.EnsureUniquePoleName(poleNames, name);
            }
            catch (InvalidOperationException e)
            {
                return Conflict(new { error = e.Message });
            }

            var id = NewPersistedOrgId("d");
            while (await db.Poles.AnyAsync(p => p.Id == id, ct))
                id = NewPersistedOrgId("d");
            db.Poles.Add(new PoleEntity { Id = id, Name = name });
            await db.SaveChangesAsync(ct);
            if (configuration.GetValue("Prime:AutoCreateMinimalOrg", false))
                await org.EnsureRootPoleHasMinimalChildrenAsync(id, ct);
            await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
            {
                NodeId = id,
                Name = name,
                Level = OrgNodeLevel.Pole,
                Code = $"POLE-{id}"
            }, ct);
            await db.SaveChangesAsync(ct);
            return Ok(new Department { Id = id, Name = name, Poles = [] });
        }

        return StatusCode(503, new { error = "Base de données non configurée." });
    }

    /// <summary><paramref name="departmentId"/> = identifiant du pôle racine (table <c>prime_pole</c>).</summary>
    [HttpPost("structure/departments/{departmentId}/poles")]
    public async Task<ActionResult<Pole>> CreatePoleForDepartment(string departmentId, [FromBody] CreateOrgNodeNameBody body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Le nom est requis." });
        var n = body.Name.Trim();
        if (db is not null)
        {
            var deptId = departmentId.Trim();
            if (!await db.Poles.AnyAsync(p => p.Id == deptId, ct))
                return NotFound(new { error = "Pôle racine introuvable." });
            var siblingNames = await db.Cellules.AsNoTracking()
                .Where(c => c.PoleId == deptId)
                .Select(c => c.Name)
                .ToListAsync(ct);
            try
            {
                OrgStructureRules.EnsureUniqueCelluleName(siblingNames, n);
            }
            catch (InvalidOperationException e)
            {
                return Conflict(new { error = e.Message });
            }

            var id = NewPersistedOrgId("p");
            while (await db.Cellules.AnyAsync(c => c.Id == id, ct))
                id = NewPersistedOrgId("p");
            db.Cellules.Add(new CelluleEntity { Id = id, Name = n, PoleId = deptId });
            await db.SaveChangesAsync(ct);
            await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
            {
                NodeId = id,
                Name = n,
                Level = OrgNodeLevel.Cellule,
                ParentNodeId = deptId,
                Code = $"CELL-{id}"
            }, ct);
            await db.SaveChangesAsync(ct);
            return Ok(new Pole { Id = id, Name = n, PoleId = deptId, Cellules = [] });
        }

        return StatusCode(503, new { error = "Base de données non configurée." });
    }

    /// <summary><paramref name="celluleId"/> = identifiant de la cellule (table <c>prime_cellule</c>) ; crée un service feuille.</summary>
    [HttpPost("structure/poles/{celluleId}/cellules")]
    public async Task<ActionResult<Cellule>> CreateCelluleForPole(string celluleId, [FromBody] CreateOrgNodeNameBody body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Le nom est requis." });
        var n = body.Name.Trim();
        if (db is not null)
        {
            var parentCelluleId = celluleId.Trim();
            if (!await db.Cellules.AnyAsync(c => c.Id == parentCelluleId, ct))
                return NotFound(new { error = "Cellule introuvable." });
            var siblingNames = await db.Services.AsNoTracking()
                .Where(s => s.CelluleId == parentCelluleId)
                .Select(s => s.Name)
                .ToListAsync(ct);
            try
            {
                OrgStructureRules.EnsureUniqueServiceName(siblingNames, n);
            }
            catch (InvalidOperationException e)
            {
                return Conflict(new { error = e.Message });
            }

            var id = NewPersistedOrgId("c");
            while (await db.Services.AnyAsync(s => s.Id == id, ct))
                id = NewPersistedOrgId("c");
            db.Services.Add(new ServiceEntity { Id = id, Name = n, CelluleId = parentCelluleId });
            await db.SaveChangesAsync(ct);
            await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
            {
                NodeId = id,
                Name = n,
                Level = OrgNodeLevel.Service,
                ParentNodeId = parentCelluleId,
                Code = $"SVC-{id}"
            }, ct);
            await db.SaveChangesAsync(ct);
            return Ok(new Cellule
            {
                Id = id,
                Name = n,
                CelluleId = parentCelluleId,
                Services =
                [
                    new Team { Id = id + "-team", Name = n, CelluleId = parentCelluleId, ServiceId = id },
                ],
            });
        }

        return StatusCode(503, new { error = "Base de données non configurée." });
    }

    [HttpPost("structure/departments/{poleId}/manager")]
    public async Task<IActionResult> SetManagerForDepartment(string poleId, [FromBody] SetOrgResponsibleBody body, CancellationToken ct)
    {
        try
        {
            if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
                return BadRequest(new { error = "employeeId est requis." });
            await ExecuteOrgStructureMutationAsync(ct,
                () => orgCommands.AssignManagerEtageAsync(body.EmployeeId, poleId, ct),
                afterMutationAsync: () => PublishStructureAssignmentAsync(
                    OrgAssignmentKind.ChefDeProjet, poleId, OrgNodeLevel.Pole, body.EmployeeId.Trim(), false, ct));
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/departments/{poleId}/manager")]
    public async Task<IActionResult> ClearManagerForDepartment(string poleId, CancellationToken ct)
    {
        await ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.ClearManagerForPoleAsync(poleId, ct),
            afterMutationAsync: () => PublishStructureAssignmentAsync(
                OrgAssignmentKind.ChefDeProjet, poleId, OrgNodeLevel.Pole, string.Empty, true, ct));
        return NoContent();
    }

    [HttpPost("structure/poles/{celluleId}/supervisor")]
    public async Task<IActionResult> SetSupervisorForPole(string celluleId, [FromBody] SetOrgResponsibleBody body, CancellationToken ct)
    {
        try
        {
            if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
                return BadRequest(new { error = "employeeId est requis." });
            await ExecuteOrgStructureMutationAsync(ct,
                () => orgCommands.AssignSupervisorServiceAsync(body.EmployeeId, celluleId, ct),
                afterMutationAsync: () => PublishStructureAssignmentAsync(
                    OrgAssignmentKind.Superviseur, celluleId, OrgNodeLevel.Cellule, body.EmployeeId.Trim(), false, ct));
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/poles/{celluleId}/supervisor")]
    public async Task<IActionResult> ClearSupervisorForPole(string celluleId, CancellationToken ct)
    {
        await ExecuteOrgStructureMutationAsync(ct, () => orgCommands.ClearSupervisorForCelluleAsync(celluleId, ct));
        return NoContent();
    }

    [HttpPost("structure/cellules/{serviceId}/coach")]
    public async Task<IActionResult> SetCoachForCellule(string serviceId, [FromBody] SetOrgResponsibleBody body, CancellationToken ct)
    {
        try
        {
            if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
                return BadRequest(new { error = "employeeId est requis." });
            await ExecuteOrgStructureMutationAsync(ct,
                () => orgCommands.AssignCoachSousServiceAsync(body.EmployeeId, serviceId, ct),
                afterMutationAsync: () => PublishStructureAssignmentAsync(
                    OrgAssignmentKind.ReferentTechnique, serviceId, OrgNodeLevel.Service, body.EmployeeId.Trim(), false, ct));
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/cellules/{serviceId}/coach")]
    public async Task<IActionResult> ClearCoachForCellule(string serviceId, CancellationToken ct)
    {
        await ExecuteOrgStructureMutationAsync(ct, () => orgCommands.ClearCoachForServiceAsync(serviceId, ct));
        return NoContent();
    }

    [HttpPost("structure/cellules/{serviceId}/pilots")]
    public async Task<IActionResult> AddPilotToCellule(string serviceId, [FromBody] AddPilotToServiceBody body, CancellationToken ct)
    {
        try
        {
            if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
                return BadRequest(new { error = "employeeId est requis." });
            var teamKey = body.TeamId ?? body.ServiceId;
            await ExecuteOrgStructureMutationAsync(ct,
                () => orgCommands.AddPilotToServiceAsync(body.EmployeeId, serviceId, ct));
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/cellules/{serviceId}/pilots/{employeeId}")]
    public async Task<IActionResult> RemovePilotFromCellule(string serviceId, string employeeId, CancellationToken ct)
    {
        try
        {
            await ExecuteOrgStructureMutationAsync(ct,
                () => orgCommands.RemovePilotFromServiceAsync(employeeId, serviceId, ct));
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }
}

[ApiController]
[Route("api/supervisor")]
public class SupervisorPrimeController(PrimeAdminDbReadService admin) : ControllerBase
{
    [HttpGet("primes")]
    public ActionResult<List<SupervisorPrimeRow>> GetPrimes([FromQuery] string supervisorUserId, [FromQuery] string? period)
    {
        try
        {
            return Ok(admin.GetSupervisorPrimes(supervisorUserId, period));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpPost("validate")]
    public ActionResult<SupervisorPrimeRow> Validate([FromBody] SupervisorValidateRequest req)
    {
        try
        {
            return Ok(admin.ValidateAsSupervisor(req.SupervisorUserId, req.ResultId));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpPost("reject")]
    public ActionResult<SupervisorPrimeRow> Reject([FromBody] SupervisorRejectRequest req)
    {
        try
        {
            return Ok(admin.RejectAsSupervisor(req.SupervisorUserId, req.ResultId));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpPost("calculate")]
    public ActionResult<SupervisorCalculateResponse> Calculate([FromBody] SupervisorCalculateRequest req)
    {
        try
        {
            return Ok(admin.ComputePrimeSupervisor(req));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpGet("dashboard")]
    public ActionResult<SupervisorDashboardResponse> GetDashboard([FromQuery] string supervisorUserId)
    {
        try
        {
            return Ok(admin.GetSupervisorDashboard(supervisorUserId));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }
}

[ApiController]
[Route("api/prime/config")]
public class PrimeConfigController(PrimeAdminDbReadService admin) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<PrimeConfigItem>> GetConfigs(
        [FromQuery] string? kind,
        [FromQuery] string? sector,
        [FromQuery] string? groupCode,
        [FromQuery] string? activityType)
        => Ok(admin.GetPrimeConfigs(kind, sector, groupCode, activityType));

    [HttpPost]
    public ActionResult<PrimeConfigItem> CreateConfig([FromBody] PrimeConfigUpsertRequest req)
        => Ok(admin.CreatePrimeConfig(req));

    [HttpPut("{id}")]
    public ActionResult<PrimeConfigItem> UpdateConfig(string id, [FromBody] PrimeConfigUpsertRequest req)
        => Ok(admin.UpdatePrimeConfig(id, req));

    [HttpDelete("{id}")]
    public ActionResult DeleteConfig(string id)
    {
        admin.DeletePrimeConfig(id);
        return NoContent();
    }
}
