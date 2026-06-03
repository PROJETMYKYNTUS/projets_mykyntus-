using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;

namespace ParrainageBackend.Services;

/// <summary>
/// Passe les dossiers approuvés en attente de confirmation RH quand la période minimum est écoulée.
/// </summary>
public sealed class ReferralEligibilityService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReferralEligibilityService> logger)
{
    public async Task<int> ProcessEligibleReferralsAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParrainageDbContext>();
        var workflow = scope.ServiceProvider.GetRequiredService<ReferralWorkflowService>();

        var now = DateTimeOffset.UtcNow;
        var candidates = await db.Referrals
            .Where(r => r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.NotEligible)
            .ToListAsync(ct);

        var pending = candidates
            .Where(r => r.EligibleForPaymentAt != null && r.EligibleForPaymentAt <= now)
            .ToList();

        if (pending.Count == 0) return 0;

        foreach (var referral in pending)
            workflow.MarkAwaitingRhConfirmation(referral, now);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : {Count} dossier(s) en attente de confirmation RH.", pending.Count);
        return pending.Count;
    }
}

public sealed class ReferralEligibilityHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReferralEligibilityHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var eligibility = scope.ServiceProvider.GetRequiredService<ReferralEligibilityService>();
                await eligibility.ProcessEligibleReferralsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "PARRAINAGE : échec scan éligibilité primes.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
