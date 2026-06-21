using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;

namespace ParrainageBackend.Services;

/// <summary>
/// Passe les dossiers approuvés en attente de confirmation RH quand la période minimum est écoulée.
/// Notifie la RH quand la date de fin de formation est atteinte.
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var candidates = await db.Referrals
            .Where(r => r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.NotEligible)
            .ToListAsync(ct);

        var pending = candidates
            .Where(r => r.EligibleForPaymentAt != null && r.EligibleForPaymentAt <= now)
            .ToList();

        foreach (var referral in pending)
            workflow.MarkAwaitingRhConfirmation(referral, now);

        var trainingDue = await db.Referrals
            .Where(r => r.Status == "IN_TRAINING"
                        && r.TrainingEndDate != null
                        && r.TrainingEndDate <= today
                        && r.TrainingEndNotifiedAt == null)
            .ToListAsync(ct);

        foreach (var referral in trainingDue)
            workflow.MarkTrainingEndDue(referral, now);

        var total = pending.Count + trainingDue.Count;
        if (total > 0)
        {
            await db.SaveChangesAsync(ct);
            if (pending.Count > 0)
                logger.LogInformation("PARRAINAGE : {Count} dossier(s) en attente de confirmation RH.", pending.Count);
            if (trainingDue.Count > 0)
                logger.LogInformation("PARRAINAGE : {Count} dossier(s) fin de formation à traiter.", trainingDue.Count);
        }

        return total;
    }

    public Task<int> ProcessTrainingEndDueAsync(CancellationToken ct = default) =>
        ProcessEligibleReferralsAsync(ct);
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
