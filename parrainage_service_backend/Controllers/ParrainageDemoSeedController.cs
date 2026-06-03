using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

/// <summary>Diagnostic et déclenchement manuel du seed démo (dev / Docker).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/parrainage/dev")]
public sealed class ParrainageDemoSeedController(
    ParrainageDbContext? db,
    IConfiguration configuration,
    ILogger<ParrainageDemoSeedController> logger) : ControllerBase
{
    [HttpGet("seed-status")]
    public async Task<ActionResult<object>> GetStatus(CancellationToken ct)
    {
        if (db is null)
        {
            return Ok(new
            {
                databaseConfigured = false,
                message = "ConnectionStrings:DefaultConnection absente.",
            });
        }

        return Ok(new
        {
            databaseConfigured = true,
            seedDemoDataEnabled = configuration.GetValue("Parrainage:SeedDemoData", false),
            allowDemoSeedEndpoint = configuration.GetValue("Parrainage:AllowDemoSeedEndpoint", true),
            referralCount = await db.Referrals.CountAsync(ct),
            ruleCount = await db.ReferralRules.CountAsync(ct),
            notificationCount = await db.ReferralNotifications.CountAsync(ct),
            hasSystemConfig = await db.SystemConfigs.AnyAsync(c => c.Id == 1, ct),
        });
    }

    [HttpPost("seed")]
    public async Task<ActionResult<object>> PostSeed(CancellationToken ct)
    {
        if (!configuration.GetValue("Parrainage:AllowDemoSeedEndpoint", true))
            return StatusCode(403, new { error = "Parrainage:AllowDemoSeedEndpoint désactivé." });

        if (db is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        await ParrainageSeeder.SeedAsync(db, logger, ct);
        return Ok(new
        {
            message = "Seed exécuté (ignoré si des parrainages existent déjà).",
            referralCount = await db.Referrals.CountAsync(ct),
        });
    }
}
