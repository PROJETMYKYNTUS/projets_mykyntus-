using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>Téléchargement direct du fichier Excel de synthèse période (RH, Manager, Comptable, Admin).</summary>
[ApiController]
[Route("api/prime/reports")]
public sealed class PrimePeriodRecapReportsController(PrimeDbContext? db) : ControllerBase
{
    private async Task<string?> RoleOfUserAsync(string userId, CancellationToken ct)
    {
        if (db == null) return null;
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Trim(), ct);
        return e?.Role;
    }

    [HttpGet("period-primes-recap.xlsx")]
    public async Task<IActionResult> DownloadPeriodRecap(
        [FromQuery] string period,
        [FromQuery] string actingUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(period)) return BadRequest(new { error = "period est requis." });
        if (string.IsNullOrWhiteSpace(actingUserId)) return BadRequest(new { error = "actingUserId est requis." });

        var uid = actingUserId.Trim();
        var role = await RoleOfUserAsync(uid, ct);
        var allow = string.Equals(role, "Admin", StringComparison.Ordinal) ||
                    string.Equals(role, "RH", StringComparison.Ordinal) ||
                    string.Equals(role, "Manager", StringComparison.Ordinal) ||
                    string.Equals(role, "Comptable", StringComparison.Ordinal);
        if (!allow)
            return StatusCode(403, new { error = "Rôle non autorisé (Admin, RH, Manager ou Comptable)." });

        var bytes = await PrimeGlobalRecapExcelBuilder.BuildAsync(db, period.Trim(), ct);
        var fileName = $"PRIME_synthese_globale_{period.Trim()}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
