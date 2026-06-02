using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/anomalies")]
public sealed class AnomaliesController(ParrainageDbContext db) : ControllerBase
{
    /// <summary>Mirror of detectAnomalies in referral.service.ts (duplicate candidate emails).</summary>
    [HttpGet]
    public async Task<ActionResult<AnomaliesDto>> Get(CancellationToken ct)
    {
        var referrals = await db.Referrals.AsNoTracking().ToListAsync(ct);

        var byEmail = referrals
            .GroupBy(r => r.CandidateEmail.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        var result = new AnomaliesDto
        {
            DuplicateCandidates = byEmail.Select(g => new DuplicateCandidateDto
            {
                Email = g.Key,
                Referrals = g.Select(r => r.ToDto()).ToList(),
            }).ToList(),
            SuspiciousEmails = byEmail.Select(g => new SuspiciousEmailDto
            {
                Email = g.Key,
                Count = g.Count(),
                ReferralIds = g.Select(r => r.Id).ToList(),
            }).ToList(),
        };

        return Ok(result);
    }
}
