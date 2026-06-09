using Microsoft.AspNetCore.Mvc;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/fiche-templates")]
public sealed class PrimeFicheTemplateController(PrimeFicheTemplateReferenceService? refService) : ControllerBase
{
    [HttpGet("{templateId}/usage")]
    public async Task<ActionResult<PrimeFicheTemplateUsageDto>> GetUsage(
        string templateId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (refService is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(templateId))
            return BadRequest(new { error = "templateId est requis." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        return Ok(await refService.GetUsageAsync(templateId, supervisorUserId, role, ct));
    }
}
