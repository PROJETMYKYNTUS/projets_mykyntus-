namespace PrimeBackend.Controllers;

using Microsoft.AspNetCore.Mvc;
using PrimeBackend.Models;
using PrimeBackend.Dto;
using PrimeBackend.Services;

[ApiController]
[Route("api/prime")]
public class PrimeController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("departments")]
    public ActionResult<List<Department>> GetDepartments() => store.GetDepartments();

    [HttpGet("employees")]
    public ActionResult<List<Employee>> GetEmployees() => store.GetEmployees();

    [HttpGet("types")]
    public ActionResult<List<PrimeType>> GetPrimeTypes() => store.GetPrimeTypes();

    [HttpGet("rules")]
    public ActionResult<List<PrimeRule>> GetPrimeRules() => store.GetPrimeRules();

    [HttpGet("results")]
    public ActionResult<List<PrimeResult>> GetPrimeResults() => store.GetPrimeResults();

    [HttpGet("my-results")]
    public ActionResult<List<PrimeResult>> GetMyPrimeResults([FromQuery] string employeeId) =>
        store.GetMyPrimeResults(employeeId);

    [HttpPut("results/{id}/status")]
    public ActionResult<PrimeResult> UpdatePrimeResultStatus(string id, [FromBody] UpdatePrimeResultStatusRequest req)
    {
        try
        {
            var updated = store.UpdatePrimeResultStatus(id, req.Status, req.ApprovedBy);
            return Ok(updated);
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpGet("dashboard-stats")]
    public ActionResult<object> GetDashboardStats() => store.GetPrimeDashboardStats();
}

[ApiController]
[Route("api/rp")]
public class RpPrimeController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("assigned-project-ids")]
    public ActionResult<List<string>> GetAssignedProjectIds([FromQuery] string rpUserId) =>
        store.GetAssignedProjectIds(rpUserId);

    [HttpGet("dashboard-stats")]
    public ActionResult<RpDashboardStats> GetRpDashboardStats([FromQuery] string rpUserId) =>
        store.GetRpDashboardStats(rpUserId);

    [HttpGet("team-performance")]
    public ActionResult<List<RpTeamMemberPerformance>> GetTeamPerformanceByProject([FromQuery] string rpUserId) =>
        store.GetTeamPerformanceByProject(rpUserId);

    [HttpGet("manager-validated")]
    public ActionResult<List<RpValidationItem>> GetManagerValidatedPrimes([FromQuery] string rpUserId) =>
        store.GetManagerValidatedPrimes(rpUserId);

    [HttpPut("validations/{id}/status")]
    public ActionResult<RpValidationItem> UpdateRpValidationStatus(string id, [FromBody] UpdateRpValidationStatusRequest req)
    {
        var updated = store.UpdateRpValidationStatus(id, req.Status);
        return Ok(updated);
    }
}

[ApiController]
[Route("api/admin")]
public class AdminPrimeController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<AdminDashboardResponse> GetDashboard() => store.GetAdminDashboard();

    [HttpGet("calculation-config")]
    public ActionResult<AdminCalculationConfig> GetCalculationConfig() => store.GetCalculationConfig();

    [HttpPut("calculation-config")]
    public ActionResult<AdminCalculationConfig> SaveCalculationConfig([FromBody] AdminCalculationConfig payload) =>
        store.SaveCalculationConfig(payload);

    [HttpGet("rbac-matrix")]
    public ActionResult<List<AdminRbacRow>> GetRbacMatrix() => store.GetRbacMatrix();

    [HttpPut("rbac-matrix/toggle")]
    public ActionResult<List<AdminRbacRow>> ToggleRbacPermission([FromBody] ToggleRbacPermissionRequest req) =>
        store.ToggleRbacPermission(req.Role, req.Permission);

    [HttpGet("workflow-config")]
    public ActionResult<AdminWorkflowConfig> GetWorkflowConfig() => store.GetWorkflowConfig();

    [HttpPut("workflow-config")]
    public ActionResult<AdminWorkflowConfig> SaveWorkflowConfig([FromBody] AdminWorkflowConfig payload) =>
        store.SaveWorkflowConfig(payload);

    [HttpGet("audit-logs")]
    public ActionResult<List<AdminAuditLog>> GetAuditLogs() => store.GetAuditLogs();

    [HttpGet("anomalies")]
    public ActionResult<List<AdminAnomaly>> GetAnomalies() => store.GetAdminAnomalies();

    [HttpPut("anomalies/{id}/status")]
    public ActionResult<List<AdminAnomaly>> UpdateAnomalyStatus(string id, [FromBody] UpdateAnomalyStatusRequest req) =>
        store.UpdateAnomalyStatus(id, req.Status);
}

[ApiController]
[Route("api/audit")]
public class AuditPrimeController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<AuditDashboardResponse> GetDashboard() => store.GetAuditDashboard();

    [HttpGet("operations")]
    public ActionResult<List<AuditOperation>> GetOperations() => store.GetOperations();

    [HttpGet("trail-logs")]
    public ActionResult<List<AuditTrailLog>> GetAuditTrailLogs() => store.GetAuditTrailLogs();

    [HttpGet("anomalies")]
    public ActionResult<List<AuditAnomaly>> GetAnomalies() => store.GetAuditAnomalies();
}

[ApiController]
[Route("api/prime/org")]
public class PrimeOrgAssignmentsController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("etages")]
    public ActionResult<List<EtageNode>> GetEtages() => store.GetEtages();

    [HttpGet("services")]
    public ActionResult<List<ServiceNode>> GetServices() => store.GetServices();

    [HttpGet("sous-services")]
    public ActionResult<List<SousServiceNode>> GetSousServices() => store.GetSousServices();

    [HttpGet("assignments/manager-etage")]
    public ActionResult<List<ManagerEtageAssignment>> GetManagerEtageAssignments([FromQuery] string? userId) =>
        store.GetManagerEtageAssignments(userId);

    [HttpGet("assignments/supervisor-service")]
    public ActionResult<List<SupervisorServiceAssignment>> GetSupervisorServiceAssignments([FromQuery] string? userId) =>
        store.GetSupervisorServiceAssignments(userId);

    [HttpGet("assignments/coach-sous-service")]
    public ActionResult<List<CoachSousServiceAssignment>> GetCoachSousServiceAssignments([FromQuery] string? userId) =>
        store.GetCoachSousServiceAssignments(userId);

    [HttpGet("assignments/coach-pilot")]
    public ActionResult<List<CoachPilotLink>> GetCoachPilotLinks([FromQuery] string? coachUserId) =>
        store.GetCoachPilotLinks(coachUserId);

    [HttpPost("assignments/manager-etage")]
    public ActionResult<ManagerEtageAssignment> AssignManagerEtage([FromBody] AssignManagerEtageRequest req)
    {
        try
        {
            return Ok(store.AssignManagerEtage(req.UserId, req.EtageId));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/supervisor-service")]
    public ActionResult<SupervisorServiceAssignment> AssignSupervisorService([FromBody] AssignSupervisorServiceRequest req)
    {
        try
        {
            return Ok(store.AssignSupervisorService(req.UserId, req.ServiceId));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/coach-sous-service")]
    public ActionResult<CoachSousServiceAssignment> AssignCoachSousService([FromBody] AssignCoachSousServiceRequest req)
    {
        try
        {
            return Ok(store.AssignCoachSousService(req.UserId, req.SousServiceId));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("assignments/coach-pilot")]
    public ActionResult<CoachPilotLink> AssignCoachPilot([FromBody] AssignCoachPilotRequest req)
    {
        try
        {
            return Ok(store.AssignCoachPilot(req.CoachUserId, req.PilotUserId));
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("assignments/manager-etage/{assignmentId}")]
    public IActionResult RemoveManagerEtageAssignment(string assignmentId)
    {
        try
        {
            store.RemoveManagerEtageAssignment(assignmentId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/supervisor-service/{assignmentId}")]
    public IActionResult RemoveSupervisorServiceAssignment(string assignmentId)
    {
        try
        {
            store.RemoveSupervisorServiceAssignment(assignmentId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/coach-sous-service/{assignmentId}")]
    public IActionResult RemoveCoachSousServiceAssignment(string assignmentId)
    {
        try
        {
            store.RemoveCoachSousServiceAssignment(assignmentId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("assignments/coach-pilot/{linkId}")]
    public IActionResult RemoveCoachPilotLink(string linkId)
    {
        try
        {
            store.RemoveCoachPilotLink(linkId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpPost("structure/departments")]
    public ActionResult<Department> CreateDepartment([FromBody] CreateOrgDepartmentBody body)
    {
        try
        {
            return Ok(store.CreateOrgDepartment(body.Name));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("structure/departments/{departmentId}/poles")]
    public ActionResult<Pole> CreatePoleForDepartment(string departmentId, [FromBody] CreateOrgNodeNameBody body)
    {
        try
        {
            return Ok(store.CreateOrgPole(departmentId, body.Name));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }

    [HttpPost("structure/poles/{poleId}/cellules")]
    public ActionResult<Cellule> CreateCelluleForPole(string poleId, [FromBody] CreateOrgNodeNameBody body)
    {
        try
        {
            return Ok(store.CreateOrgCellule(poleId, body.Name));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }

    [HttpPost("structure/departments/{departmentId}/manager")]
    public IActionResult SetManagerForDepartment(string departmentId, [FromBody] SetOrgResponsibleBody body)
    {
        try
        {
            store.SetManagerForDepartment(body.EmployeeId, departmentId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/departments/{departmentId}/manager")]
    public IActionResult ClearManagerForDepartment(string departmentId)
    {
        store.ClearManagerForDepartment(departmentId);
        return NoContent();
    }

    [HttpPost("structure/poles/{poleId}/supervisor")]
    public IActionResult SetSupervisorForPole(string poleId, [FromBody] SetOrgResponsibleBody body)
    {
        try
        {
            store.SetSupervisorForPole(body.EmployeeId, poleId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/poles/{poleId}/supervisor")]
    public IActionResult ClearSupervisorForPole(string poleId)
    {
        store.ClearSupervisorForPole(poleId);
        return NoContent();
    }

    [HttpPost("structure/cellules/{celluleId}/coach")]
    public IActionResult SetCoachForCellule(string celluleId, [FromBody] SetOrgResponsibleBody body)
    {
        try
        {
            store.SetCoachForCellule(body.EmployeeId, celluleId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/cellules/{celluleId}/coach")]
    public IActionResult ClearCoachForCellule(string celluleId)
    {
        store.ClearCoachForCellule(celluleId);
        return NoContent();
    }

    [HttpPost("structure/cellules/{celluleId}/pilots")]
    public IActionResult AddPilotToCellule(string celluleId, [FromBody] AddPilotToCelluleBody body)
    {
        try
        {
            store.AddPilotToCellule(body.EmployeeId, celluleId, body.TeamId);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("structure/cellules/{celluleId}/pilots/{employeeId}")]
    public IActionResult RemovePilotFromCellule(string celluleId, string employeeId)
    {
        try
        {
            store.RemovePilotFromCellule(employeeId, celluleId);
            return NoContent();
        }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }
}

[ApiController]
[Route("api/supervisor")]
public class SupervisorPrimeController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet("primes")]
    public ActionResult<List<SupervisorPrimeRow>> GetPrimes([FromQuery] string supervisorUserId, [FromQuery] string? period)
    {
        try
        {
            return Ok(store.GetSupervisorPrimes(supervisorUserId, period));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpPost("validate")]
    public ActionResult<SupervisorPrimeRow> Validate([FromBody] SupervisorValidateRequest req)
    {
        try
        {
            return Ok(store.ValidateAsSupervisor(req.SupervisorUserId, req.ResultId));
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
            return Ok(store.RejectAsSupervisor(req.SupervisorUserId, req.ResultId));
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
            return Ok(store.ComputePrimeSupervisor(req));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpGet("dashboard")]
    public ActionResult<SupervisorDashboardResponse> GetDashboard([FromQuery] string supervisorUserId)
    {
        try
        {
            return Ok(store.GetSupervisorDashboard(supervisorUserId));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }
}

[ApiController]
[Route("api/prime/config")]
public class PrimeConfigController(PrimeInMemoryStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<PrimeConfigItem>> GetConfigs(
        [FromQuery] string? kind,
        [FromQuery] string? sector,
        [FromQuery] string? groupCode,
        [FromQuery] string? activityType)
        => Ok(store.GetPrimeConfigs(kind, sector, groupCode, activityType));

    [HttpPost]
    public ActionResult<PrimeConfigItem> CreateConfig([FromBody] PrimeConfigUpsertRequest req)
        => Ok(store.CreatePrimeConfig(req));

    [HttpPut("{id}")]
    public ActionResult<PrimeConfigItem> UpdateConfig(string id, [FromBody] PrimeConfigUpsertRequest req)
        => Ok(store.UpdatePrimeConfig(id, req));

    [HttpDelete("{id}")]
    public ActionResult DeleteConfig(string id)
    {
        store.DeletePrimeConfig(id);
        return NoContent();
    }
}
