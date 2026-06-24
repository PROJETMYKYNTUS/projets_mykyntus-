using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/admin/org-reconciliation")]
[Authorize(Roles = "Admin,RH,Manager,Superviseur,Coach,Pilote")]
public sealed class OrgReconciliationController(
    IOrgReconciliationService reconciliation,
    ILogger<OrgReconciliationController> logger) : ControllerBase
{
    [HttpPost("backfill-from-prime")]
    public async Task<IActionResult> BackfillFromPrime([FromBody] PrimeOrgBackfillRequest request, CancellationToken ct)
    {
        var count = await reconciliation.BackfillFromPrimeAsync(request, ct);
        logger.LogInformation("Org reconciliation manual: {Count} actions", count);
        return Ok(new { count });
    }

    [HttpPost("sync-from-prime")]
    public async Task<IActionResult> SyncFromPrime(CancellationToken ct)
    {
        var verify = await reconciliation.SyncFromPrimeAsync(ct);
        return Ok(verify);
    }

    [HttpPost("sync-from-directory")]
    public async Task<IActionResult> SyncFromDirectory(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.ToString();
        var verify = await reconciliation.SyncFromDirectoryAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);
        logger.LogInformation(
            "Org mirror sync-from-directory: ok={Ok}",
            verify.Ok);
        return Ok(verify);
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct)
    {
        var verify = await reconciliation.VerifyAsync(ct);
        return Ok(new
        {
            floorsWithoutPrimeId = verify.FloorsWithoutPrimeId,
            servicesWithoutPrimeCelluleId = verify.ServicesWithoutPrimeCelluleId,
            subServicesWithoutPrimeServiceId = verify.SubServicesWithoutPrimeServiceId,
            duplicateSubServiceNames = verify.DuplicateSubServiceNames,
            activeUsers = verify.ActiveUsers,
            ok = verify.Ok,
        });
    }
}
