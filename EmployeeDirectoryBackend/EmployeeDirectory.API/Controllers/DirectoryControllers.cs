using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using Kyntus.Iam;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.API.Controllers;

[ApiController]
[Route("api/directory")]
[Authorize]
public class DirectoryEmployeesController(IDirectoryReadService read, IDirectoryWriteService write) : ControllerBase
{
    [HttpGet("employees")]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(
        [FromQuery] string? role,
        [FromQuery] string? poleId,
        CancellationToken ct) =>
        Ok(await read.GetEmployeesAsync(role, poleId, ct));

    [HttpGet("employees/check-email")]
    public async Task<ActionResult<object>> CheckEmail([FromQuery] string email, [FromQuery] Guid? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "email requis." });
        var isUsed = await read.IsEmailUsedAsync(email, excludeId, ct);
        return Ok(new { isUnique = !isUsed });
    }

    [HttpGet("employees/{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Get(Guid id, CancellationToken ct)
    {
        var e = await read.GetEmployeeByIdAsync(id, ct);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpPost("employees")]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest body, CancellationToken ct)
    {
        var changedBy = User.GetSubjectId();
        try
        {
            var result = await write.CreateEmployeeAsync(body, changedBy, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("employees/{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, [FromBody] UpdateEmployeeRequest body, CancellationToken ct)
    {
        var changedBy = User.GetSubjectId();
        var result = await write.UpdateEmployeeAsync(id, body, changedBy, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("employees/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var changedBy = User.GetSubjectId();
        return await write.DeleteEmployeeAsync(id, changedBy, ct) ? NoContent() : NotFound();
    }

    [HttpPatch("employees/{id:guid}/auth-subject")]
    public async Task<IActionResult> SetAuthSubject(Guid id, [FromBody] SetAuthSubjectRequest body, CancellationToken ct)
    {
        if (body.AuthSubjectId == Guid.Empty)
            return BadRequest(new { error = "authSubjectId requis" });
        return await write.SetAuthSubjectIdAsync(id, body.AuthSubjectId, ct) ? NoContent() : NotFound();
    }

    [HttpGet("employees/{id:guid}/assignment-history")]
    public async Task<ActionResult<IReadOnlyList<AssignmentHistoryEntryDto>>> History(Guid id, CancellationToken ct) =>
        Ok(await read.GetAssignmentHistoryAsync(id, ct));
}

[ApiController]
[Route("api/directory/org")]
[Authorize]
public class DirectoryOrgController(
    IDirectoryReadService read,
    IDirectoryWriteService write,
    ILogger<DirectoryOrgController> logger) : ControllerBase
{
    [HttpGet("overview")]
    [AllowAnonymous]
    public async Task<ActionResult<OrgOverviewDto>> Overview(CancellationToken ct) =>
        Ok(await read.GetOrgOverviewAsync(ct));

    [HttpGet("assignments/as-of")]
    [AllowAnonymous]
    public async Task<ActionResult<OrgAssignmentAsOfDto>> AsOf([FromQuery] DateTime date, CancellationToken ct) =>
        Ok(await read.GetAssignmentsAsOfAsync(date, ct));

    [HttpPost("structure/poles")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<object>> CreatePole([FromBody] CreatePoleRequest? body, CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Corps de requête requis." });
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Le nom du pôle est requis." });
        if (body.BusinessDepartmentId == Guid.Empty)
            return BadRequest(new { error = "businessDepartmentId requis." });

        try
        {
            return Ok(new { id = await write.CreatePoleAsync(body.Name.Trim(), body.BusinessDepartmentId, ct) });
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "CreatePole DbUpdateException (dept={DeptId}, name={Name})", body.BusinessDepartmentId, body.Name);
            var detail = ex.InnerException?.Message ?? ex.Message;
            if (ex is DbUpdateConcurrencyException)
            {
                return Conflict(new { error = "Conflit de concurrence lors de la création du pôle. Réessayez." });
            }
            if (detail.Contains("BusinessDepartmentId", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("42703", StringComparison.Ordinal))
            {
                return StatusCode(500, new
                {
                    error = "Schéma base de données obsolète (colonne BusinessDepartmentId). Redémarrez le service Directory.",
                });
            }

            if (detail.Contains("23505", StringComparison.Ordinal)
                || detail.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = "Contrainte d'unicité violée lors de la création du pôle." });
            }

            return StatusCode(500, new { error = "Erreur base de données lors de la création du pôle." });
        }
    }

    [HttpPatch("structure/poles/{nodeId}/business-department")]
    public async Task<IActionResult> AttachPoleToDepartment(string nodeId, [FromBody] AttachPoleToDepartmentRequest body, CancellationToken ct)
    {
        try
        {
            return await write.AttachPoleToBusinessDepartmentAsync(nodeId, body.BusinessDepartmentId, ct)
                ? NoContent()
                : NotFound();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpPost("structure/poles/{poleId}/cellules")]
    public async Task<ActionResult<object>> CreateCellule(string poleId, [FromBody] CreateNodeRequest body, CancellationToken ct) =>
        Ok(new { id = await write.CreateCelluleAsync(poleId, body.Name, ct) });

    [HttpPost("structure/cellules/{celluleId}/services")]
    public async Task<ActionResult<object>> CreateService(string celluleId, [FromBody] CreateNodeRequest body, CancellationToken ct) =>
        Ok(new { id = await write.CreateServiceAsync(celluleId, body.Name, ct) });

    [HttpPut("structure/poles/{nodeId}")]
    public async Task<IActionResult> RenamePole(string nodeId, [FromBody] CreateNodeRequest body, CancellationToken ct) =>
        await write.RenameOrgNodeAsync(OrgNodeLevel.Pole, nodeId, body.Name, ct) ? NoContent() : NotFound();

    [HttpPut("structure/poles/{poleId}/cellules/{nodeId}")]
    public async Task<IActionResult> RenameCellule(string poleId, string nodeId, [FromBody] CreateNodeRequest body, CancellationToken ct) =>
        await write.RenameOrgNodeAsync(OrgNodeLevel.Cellule, nodeId, body.Name, ct) ? NoContent() : NotFound();

    [HttpPut("structure/cellules/{celluleId}/services/{nodeId}")]
    public async Task<IActionResult> RenameService(string celluleId, string nodeId, [FromBody] CreateNodeRequest body, CancellationToken ct) =>
        await write.RenameOrgNodeAsync(OrgNodeLevel.Service, nodeId, body.Name, ct) ? NoContent() : NotFound();

    [HttpDelete("structure/poles/{nodeId}")]
    public async Task<IActionResult> DeletePole(string nodeId, CancellationToken ct) =>
        await write.DeleteOrgNodeAsync(OrgNodeLevel.Pole, nodeId, ct) ? NoContent() : NotFound();

    [HttpDelete("structure/poles/{poleId}/cellules/{nodeId}")]
    public async Task<IActionResult> DeleteCellule(string poleId, string nodeId, CancellationToken ct) =>
        await write.DeleteOrgNodeAsync(OrgNodeLevel.Cellule, nodeId, ct) ? NoContent() : NotFound();

    [HttpDelete("structure/cellules/{celluleId}/services/{nodeId}")]
    public async Task<IActionResult> DeleteService(string celluleId, string nodeId, CancellationToken ct) =>
        await write.DeleteOrgNodeAsync(OrgNodeLevel.Service, nodeId, ct) ? NoContent() : NotFound();

    [HttpPost("assignments/{kind}/{nodeId}")]
    public async Task<ActionResult<StructuralRoleAssignmentResult>> Assign(string kind, string nodeId, [FromBody] AssignRequest body, CancellationToken ct)
    {
        if (!Guid.TryParse(body.EmployeeId, out var employeeId))
            return BadRequest(new { error = "employeeId invalide" });
        var changedBy = User.GetSubjectId();
        var result = await write.AssignStructureRoleAsync(kind, nodeId, employeeId, changedBy, body.Reason, ct);
        return Ok(result);
    }

    [HttpDelete("assignments/Pilote/{serviceId}/employees/{employeeId:guid}")]
    public async Task<IActionResult> RemovePilot(string serviceId, Guid employeeId, CancellationToken ct)
    {
        var changedBy = User.GetSubjectId();
        return await write.RemoveStructurePilotAsync(serviceId, employeeId, changedBy, null, ct)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("assignments/{kind}/{nodeId}")]
    public async Task<IActionResult> Clear(string kind, string nodeId, CancellationToken ct)
    {
        var changedBy = User.GetSubjectId();
        await write.ClearStructureRoleAsync(kind, nodeId, changedBy, null, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/directory/rebac")]
[Authorize]
public class DirectoryRebacController(IDirectoryReadService read) : ControllerBase
{
    [HttpGet("is-descendant")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> IsDescendant([FromQuery] Guid viewerId, [FromQuery] Guid targetId, CancellationToken ct) =>
        Ok(new { isDescendant = await read.IsDescendantAsync(viewerId, targetId, ct) });

    [HttpGet("managed-nodes")]
    [AllowAnonymous]
    public async Task<ActionResult<RebacManagedNodesDto>> ManagedNodes(
        [FromQuery] Guid employeeId,
        [FromQuery] string kind,
        CancellationToken ct) =>
        Ok(await read.GetManagedNodesAsync(employeeId, kind, ct));

    [HttpGet("hierarchy/{employeeId:guid}/subtree")]
    public async Task<ActionResult<RebacSubtreeDto>> Subtree(Guid employeeId, CancellationToken ct) =>
        Ok(await read.GetSubtreeAsync(employeeId, ct));
}

[ApiController]
[Route("api/iam")]
[Authorize]
public class IamController(IIamReadService iam, IPolicyEvaluator evaluator) : ControllerBase
{
    [HttpGet("effective-permissions")]
    public async Task<ActionResult<EffectivePermissionsDto>> EffectivePermissions(CancellationToken ct)
    {
        var subjectId = User.GetSubjectId() ?? Guid.Empty;
        var role = User.GetAuthRole() ?? "Employee";
        return Ok(await iam.GetEffectivePermissionsAsync(subjectId, role, ct));
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<object>> Evaluate([FromBody] EvaluatePolicyRequest body, CancellationToken ct)
    {
        var subjectId = User.GetSubjectId() ?? Guid.Empty;
        var role = User.GetAuthRole() ?? "Employee";
        var decision = await evaluator.EvaluateAsync(
            new PolicyRequest(subjectId, role, body.Action, body.ResourceType, body.ResourceId), ct);
        return Ok(new { allowed = decision.Allowed, reason = decision.Reason });
    }
}

[ApiController]
[Route("api/directory/reconcile")]
public class DirectoryReconcileController(IDirectoryReconciliationService reconcile) : ControllerBase
{
    [HttpGet("verify")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<DirectoryReconcileVerifyDto>> Verify(CancellationToken ct) =>
        Ok(await reconcile.VerifyAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<DirectoryReconcileReportDto>> Reconcile(CancellationToken ct) =>
        Ok(await reconcile.ReconcileAsync(ct));
}

[ApiController]
[Route("api/directory/business-departments")]
[Authorize]
public class DirectoryBusinessDepartmentsController(IDirectoryReadService read, IDirectoryWriteService write) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessDepartmentDto>>> List(CancellationToken ct) =>
        Ok(await read.GetBusinessDepartmentsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessDepartmentDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await read.GetBusinessDepartmentByIdAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<BusinessDepartmentDto>> Create([FromBody] CreateBusinessDepartmentRequest body, CancellationToken ct)
    {
        var result = await write.CreateBusinessDepartmentAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<BusinessDepartmentDto>> Update(Guid id, [FromBody] UpdateBusinessDepartmentRequest body, CancellationToken ct)
    {
        var result = await write.UpdateBusinessDepartmentAsync(id, body, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await write.DeleteBusinessDepartmentAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/poles/{poleId}")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<IActionResult> AssignPole(Guid id, string poleId, CancellationToken ct)
    {
        try
        {
            await write.AssignPoleToBusinessDepartmentAsync(id, poleId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpDelete("{id:guid}/poles/{poleId}")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<IActionResult> RemovePole(Guid id, string poleId, CancellationToken ct) =>
        await write.RemovePoleFromBusinessDepartmentAsync(id, poleId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/manager")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<ActionResult<StructuralRoleAssignmentResult>> SetManager(Guid id, [FromBody] SetBusinessDepartmentManagerRequest body, CancellationToken ct)
    {
        if (!Guid.TryParse(body.EmployeeId, out var employeeId))
            return BadRequest(new { error = "employeeId invalide" });
        try
        {
            var changedBy = User.GetSubjectId();
            var result = await write.SetBusinessDepartmentManagerAsync(id, employeeId, changedBy, null, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
    }

    [HttpDelete("{id:guid}/manager")]
    [Authorize(Roles = "Admin,RH")]
    public async Task<IActionResult> ClearManager(Guid id, CancellationToken ct) =>
        await write.ClearBusinessDepartmentManagerAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/directory")]
public class DirectoryHealthController : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "healthy", service = "employee-directory" });
}

public record CreateNodeRequest(string Name);
public record AssignRequest(string EmployeeId, string? Reason);
public record SetAuthSubjectRequest(Guid AuthSubjectId);
public record EvaluatePolicyRequest(string Action, string ResourceType, string? ResourceId);

internal static class ClaimsExtensions
{
    public static Guid? GetSubjectId(this System.Security.Claims.ClaimsPrincipal user) =>
        Kyntus.Identity.Jwt.KyntusClaimsPrincipalExtensions.GetSubjectId(user);
    public static string? GetAuthRole(this System.Security.Claims.ClaimsPrincipal user) =>
        Kyntus.Identity.Jwt.KyntusClaimsPrincipalExtensions.GetAuthRole(user);
}
