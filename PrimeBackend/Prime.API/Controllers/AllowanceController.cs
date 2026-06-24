using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.Allowance;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/allowances")]
[Authorize]
public class AllowanceController(
    IMediator mediator,
    IAllowanceOperationsAppService? allowances,
    IAllowanceQueryAppService? allowanceQuery,
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
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!IsRhOrAdmin(role)) return Forbid();
        return Ok(await allowances.ListTypesAsync(ct));
    }

    [HttpPost("types")]
    public async Task<ActionResult<AllowanceTypeDto>> CreateType([FromBody] CreateAllowanceTypeRequest body, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (_, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!IsRhOrAdmin(role)) return Forbid();
        return Ok(await allowances.CreateTypeAsync(body, ct));
    }

    [HttpGet("types/eligible")]
    public async Task<ActionResult<List<AllowanceTypeDto>>> EligibleTypes(
        [FromQuery] string? businessDepartmentId, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (string.IsNullOrWhiteSpace(businessDepartmentId) && role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            businessDepartmentId = await allowances.GetManagerDepartmentIdAsync(userId, ct);
        return Ok(await allowances.ListEligibleTypesAsync(businessDepartmentId, ct));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<AllowanceRequestDto>>> ListRequests(
        [FromQuery] string? departmentId, [FromQuery] string? period, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        return Ok(await allowances.ListRequestsAsync(userId, role, departmentId, period, ct));
    }

    [HttpGet("requests/inbox")]
    public async Task<ActionResult<List<AllowanceRequestDto>>> Inbox(CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!AllowanceValidationRoles.IsAllowanceStakeholder(role)
            && !role.Equals("Pilote", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        return Ok(await allowances.InboxAsync(userId, role, ct));
    }

    [HttpPost("requests")]
    public async Task<ActionResult<AllowanceRequestDto>> CreateRequest(
        [FromBody] CreateAllowanceRequestBody body, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try
        {
            return Ok(await allowances.CreateRequestAsync(userId, body, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPatch("requests/{id:guid}")]
    public async Task<ActionResult<AllowanceRequestDto>> UpdateDraft(
        Guid id, [FromBody] UpdateAllowanceRequestBody body, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try { return Ok(await allowances.UpdateDraftAsync(id, userId, body, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/submit")]
    public async Task<ActionResult<AllowanceRequestDto>> Submit(Guid id, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, _, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        try { return Ok(await allowances.SubmitAsync(id, userId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<ActionResult<AllowanceRequestDto>> Approve(Guid id, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        try { return Ok(await allowances.ApproveAsync(id, userId, role, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<ActionResult<AllowanceRequestDto>> Reject(Guid id, [FromBody] RejectAllowanceBody body, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (string.IsNullOrWhiteSpace(body.Reason)) return BadRequest(new { error = "Motif requis." });
        try { return Ok(await allowances.RejectAsync(id, userId, role, body.Reason, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("rules/generate-proposals")]
    public async Task<ActionResult<object>> GenerateProposals(
        [FromQuery] string period, [FromQuery] string businessDepartmentId, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        if (!await allowances.IsSupportDepartmentManagerAsync(userId, ct))
            return Forbid();
        var managedDeptId = await allowances.GetManagerDepartmentIdAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(businessDepartmentId))
            businessDepartmentId = managedDeptId ?? "";
        if (managedDeptId is null || !string.Equals(managedDeptId, businessDepartmentId, StringComparison.Ordinal))
            return Forbid();
        var count = await allowances.GenerateProposalsAsync(period, businessDepartmentId, userId, ct);
        return Ok(new { created = count });
    }

    [HttpGet("business-departments")]
    public async Task<ActionResult<List<BusinessDepartmentMirrorDto>>> ListDepartments(CancellationToken ct)
    {
        if (allowanceQuery is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListAllowanceBusinessDepartmentsQuery(), ct));
    }

    [HttpGet("context/me")]
    public async Task<ActionResult<object>> MyContext(CancellationToken ct)
    {
        if (allowanceQuery is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        var ctx = await mediator.Send(new GetAllowanceMyContextQuery(userId, role), ct);
        return Ok(new
        {
            userId = ctx.UserId,
            role = ctx.Role,
            businessDepartmentId = ctx.BusinessDepartmentId,
            businessDepartmentKind = ctx.BusinessDepartmentKind,
            isSupportDepartmentManager = ctx.IsSupportDepartmentManager,
            isOperationalDepartmentManager = ctx.IsOperationalDepartmentManager,
            managedDepartmentId = ctx.ManagedDepartmentId,
            managedDepartmentKind = ctx.ManagedDepartmentKind,
            managedDepartmentName = ctx.ManagedDepartmentName,
            managedDepartmentCode = ctx.ManagedDepartmentCode,
            directReportCount = ctx.DirectReportCount,
        });
    }

    [HttpGet("team-progress")]
    public async Task<ActionResult<AllowanceTeamProgressDto>> TeamProgress(
        [FromQuery] string period, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(period)) return BadRequest(new { error = "Période requise." });
        try
        {
            return Ok(await allowances.GetTeamProgressAsync(userId, period, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("employee-allocations")]
    public async Task<ActionResult<AllowanceEmployeeAllocationsDto>> EmployeeAllocations(
        [FromQuery] string period, [FromQuery] string employeeId, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "Période et employeeId requis." });
        try
        {
            return Ok(await allowances.GetEmployeeAllocationsAsync(userId, employeeId, period, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("team/generate-proposals")]
    public async Task<ActionResult<object>> GenerateTeamProposals([FromQuery] string period, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(period)) return BadRequest(new { error = "Période requise." });
        if (!await allowances.IsSupportDepartmentManagerAsync(userId, ct)) return Forbid();
        var deptId = await allowances.GetManagerDepartmentIdAsync(userId, ct);
        if (deptId is null) return Forbid();
        var count = await allowances.GenerateProposalsAsync(period, deptId, userId, ct);
        return Ok(new { created = count });
    }

    [HttpPost("no-bonus")]
    public async Task<ActionResult<object>> MarkNoBonus(
        [FromQuery] string period, [FromQuery] string employeeId,
        [FromBody] MarkNoBonusBody? body, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "Période et employeeId requis." });
        try
        {
            await allowances.MarkNoBonusAsync(userId, employeeId, period, body?.Comment, ct);
            return Ok(new { marked = true });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpDelete("no-bonus")]
    public async Task<ActionResult<object>> ClearNoBonus(
        [FromQuery] string period, [FromQuery] string employeeId, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "Période et employeeId requis." });
        try
        {
            await allowances.ClearNoBonusAsync(userId, employeeId, period, ct);
            return Ok(new { cleared = true });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<AllowanceHistoryEntryDto>>> History(
        [FromQuery] string? fromPeriod, [FromQuery] string? toPeriod, CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try
        {
            return Ok(await allowances.GetHistoryAsync(userId, fromPeriod, toPeriod, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("period-summaries")]
    public async Task<ActionResult<List<AllowancePeriodSummaryDto>>> PeriodSummaries(CancellationToken ct)
    {
        if (allowances is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, role, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        if (!role.Equals("Manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
        try
        {
            return Ok(await allowances.GetPeriodSummariesAsync(userId, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("team")]
    public async Task<ActionResult<List<object>>> Team(CancellationToken ct)
    {
        if (allowanceQuery is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (userId, _, err) = await ResolveAsync(ct);
        if (err is not null) return err;
        try
        {
            return Ok(await mediator.Send(new GetAllowanceTeamQuery(userId), ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private static bool IsRhOrAdmin(string role) =>
        role.Equals("RH", StringComparison.OrdinalIgnoreCase)
        || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
}
