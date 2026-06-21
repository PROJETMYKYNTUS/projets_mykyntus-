using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/allowances")]
[Authorize]
public class AllowanceController(
    AllowanceCatalogService catalog,
    AllowanceRequestService requests,
    AllowanceScopeService scope,
    AllowanceRuleEngineService ruleEngine,
    PrimeDbContext db,
    IPrimeRequestUserResolver userResolver) : ControllerBase
{
    private async Task<(string UserId, string Role, ActionResult? Error)> ResolveAsync(CancellationToken ct)
    {
        var resolved = await userResolver.TryResolveForValidationAsync(
            Request,
            Request.Headers[IPrimeRequestUserResolver.HeaderUserId].FirstOrDefault(),
            Request.Headers[IPrimeRequestUserResolver.HeaderRole].FirstOrDefault(),
            ct);
        if (resolved is null)
            return ("", "", Unauthorized(new { error = "Utilisateur PRIME non résolu." }));
        return (resolved.UserId, resolved.Role, null);
    }

    [HttpGet("types")]
    public async Task<ActionResult<List<AllowanceTypeDto>>> ListTypes(CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!IsRhOrAdmin(role)) return Forbid();
        return Ok(await catalog.ListTypesAsync(ct));
    }

    [HttpPost("types")]
    public async Task<ActionResult<AllowanceTypeDto>> CreateType([FromBody] CreateAllowanceTypeRequest body, CancellationToken ct)
    {
        var (_, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!IsRhOrAdmin(role)) return Forbid();
        return Ok(await catalog.CreateTypeAsync(body, ct));
    }

    [HttpGet("types/eligible")]
    public async Task<ActionResult<List<AllowanceTypeDto>>> EligibleTypes(
        [FromQuery] string? businessDepartmentId, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (string.IsNullOrWhiteSpace(businessDepartmentId) && role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            businessDepartmentId = await scope.GetManagerDepartmentIdAsync(userId, ct);
        return Ok(await catalog.ListEligibleTypesAsync(businessDepartmentId, ct));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<AllowanceRequestDto>>> ListRequests(
        [FromQuery] string? departmentId, [FromQuery] string? period, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        return Ok(await requests.ListAsync(userId, role, departmentId, period, ct));
    }

    [HttpGet("requests/inbox")]
    public async Task<ActionResult<List<AllowanceRequestDto>>> Inbox(CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!AllowanceValidationRoles.IsAllowanceStakeholder(role)
            && !role.Equals("Pilote", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        return Ok(await requests.InboxAsync(userId, role, ct));
    }

    [HttpPost("requests")]
    public async Task<ActionResult<AllowanceRequestDto>> CreateRequest(
        [FromBody] CreateAllowanceRequestBody body, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try
        {
            return Ok(await requests.CreateAsync(userId, body, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPatch("requests/{id:guid}")]
    public async Task<ActionResult<AllowanceRequestDto>> UpdateDraft(
        Guid id, [FromBody] UpdateAllowanceRequestBody body, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try { return Ok(await requests.UpdateDraftAsync(id, userId, body, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/submit")]
    public async Task<ActionResult<AllowanceRequestDto>> Submit(Guid id, CancellationToken ct)
    {
        var (userId, _, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        try { return Ok(await requests.SubmitAsync(id, userId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<ActionResult<AllowanceRequestDto>> Approve(Guid id, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        try { return Ok(await requests.ApproveAsync(id, userId, role, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<ActionResult<AllowanceRequestDto>> Reject(Guid id, [FromBody] RejectAllowanceBody body, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (string.IsNullOrWhiteSpace(body.Reason)) return BadRequest(new { error = "Motif requis." });
        try { return Ok(await requests.RejectAsync(id, userId, role, body.Reason, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("rules/generate-proposals")]
    public async Task<ActionResult<object>> GenerateProposals(
        [FromQuery] string period, [FromQuery] string businessDepartmentId, CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        if (!await scope.IsSupportDepartmentManagerAsync(userId, ct))
            return Forbid();
        var managedDeptId = await scope.GetManagerDepartmentIdAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(businessDepartmentId))
            businessDepartmentId = managedDeptId ?? "";
        if (managedDeptId is null || !string.Equals(managedDeptId, businessDepartmentId, StringComparison.Ordinal))
            return Forbid();
        var count = await ruleEngine.GenerateProposalsAsync(period, businessDepartmentId, userId, ct);
        return Ok(new { created = count });
    }

    [HttpGet("business-departments")]
    public async Task<ActionResult<List<BusinessDepartmentMirrorDto>>> ListDepartments(CancellationToken ct)
    {
        var rows = await db.BusinessDepartments.AsNoTracking()
            .Include(d => d.PoleAssignments)
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
        return Ok(rows.Select(d => new BusinessDepartmentMirrorDto(
            d.Id, d.Code, d.Name, d.Kind, d.ManagerEmployeeId, d.IsActive,
            d.PoleAssignments.Select(p => p.PoleId).ToList())).ToList());
    }

    [HttpGet("context/me")]
    public async Task<ActionResult<object>> MyContext(CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == userId, ct);
        var managedDept = await db.BusinessDepartments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ManagerEmployeeId == userId && d.IsActive, ct);
        var directReportCount = await db.Employees.AsNoTracking()
            .CountAsync(e => e.ParentId == userId, ct);
        return Ok(new
        {
            userId,
            role,
            businessDepartmentId = emp?.BusinessDepartmentId,
            businessDepartmentKind = emp?.BusinessDepartmentKind,
            isSupportDepartmentManager = managedDept?.Kind == "Support",
            isOperationalDepartmentManager = managedDept?.Kind == "Operational",
            managedDepartmentId = managedDept?.Id,
            managedDepartmentKind = managedDept?.Kind,
            managedDepartmentName = managedDept?.Name,
            managedDepartmentCode = managedDept?.Code,
            directReportCount,
        });
    }

    [HttpGet("team")]
    public async Task<ActionResult<List<object>>> Team(CancellationToken ct)
    {
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!await scope.IsSupportDepartmentManagerAsync(userId, ct))
            return Forbid();
        var deptId = await scope.GetManagerDepartmentIdAsync(userId, ct);
        var query = db.Employees.AsNoTracking()
            .Where(e => e.ParentId == userId
                        && e.BusinessDepartmentKind == "Support");
        if (!string.IsNullOrWhiteSpace(deptId))
            query = query.Where(e => e.BusinessDepartmentId == deptId);
        var team = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new
            {
                id = e.Id,
                firstName = e.FirstName,
                lastName = e.LastName,
                email = e.Email,
            })
            .ToListAsync(ct);
        return Ok(team);
    }

    private static bool IsRhOrAdmin(string role) =>
        role.Equals("RH", StringComparison.OrdinalIgnoreCase)
        || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
}
