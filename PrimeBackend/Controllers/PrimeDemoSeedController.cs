using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Controllers;

/// <summary>Diagnostic et déclenchement manuel de l'enrichissement démo (dev / Docker).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/prime/demo")]
public sealed class PrimeDemoSeedController(PrimeDbContext? db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("enrichment-status")]
    public async Task<ActionResult<object>> GetEnrichmentStatus(CancellationToken ct)
    {
        if (db is null)
        {
            return Ok(new
            {
                databaseConfigured = false,
                message = "ConnectionStrings:DefaultConnection absente — aucune donnée PostgreSQL.",
            });
        }

        var counts = await PrimeDbEnrichmentSeeder.SnapshotCountsAsync(db, ct);
        var markerApplied = await PrimeDbEnrichmentSeeder.IsVersionAppliedAsync(db, ct);
        var hasData = await PrimeDbEnrichmentSeeder.HasEnrichmentDataAsync(db, ct);
        var enrichEnabled = configuration.GetValue("Prime:EnrichDemoData", false);

        return Ok(new
        {
            databaseConfigured = true,
            enrichDemoDataEnabled = enrichEnabled,
            enrichmentVersion = PrimeDbEnrichmentSeeder.Version,
            markerApplied,
            hasEnrichmentData = hasData,
            needsEnrichment = enrichEnabled && (!markerApplied || !hasData),
            counts,
            currentPeriodUtc = $"{DateTime.UtcNow:yyyy-MM}",
            hintSupervisor = "e9",
            hintChefDeProjet = "e6",
            enrichTemplateId = PrimeDbEnrichmentSeeder.EnrichTemplateId,
            enrichEmployeeIdPrefix = PrimeMoroccanDataFactory.EnrichEmployeeIdPrefix,
        });
    }

    [HttpPost("enrich")]
    public async Task<ActionResult<object>> PostEnrich([FromQuery] bool force = false, CancellationToken ct = default)
    {
        if (!configuration.GetValue("Prime:AllowDemoSeedEndpoint", true))
            return StatusCode(403, new { error = "Prime:AllowDemoSeedEndpoint désactivé." });

        if (db is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        var result = await PrimeDbEnrichmentSeeder.EnrichAsync(db, force, ct);
        var counts = await PrimeDbEnrichmentSeeder.SnapshotCountsAsync(db, ct);
        return Ok(new { result, counts });
    }
}
