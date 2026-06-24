using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class AnomalyQueryService(ParrainageDbContext db) : IAnomalyQueryService
{
    public async Task<AnomaliesDto> GetAsync(CancellationToken ct = default)
    {
        var referrals = await db.Referrals.AsNoTracking().ToListAsync(ct);

        var byEmail = referrals
            .GroupBy(r => r.CandidateEmail.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        return new AnomaliesDto
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
    }
}
