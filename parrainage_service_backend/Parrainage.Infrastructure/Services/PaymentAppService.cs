using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Domain.Entities;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class PaymentAppService(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    ReferralEligibilityService eligibility) : IPaymentAppService
{
    public async Task<PaymentInboxDto> GetInboxAsync(CancellationToken ct = default)
    {
        await eligibility.ProcessEligibleReferralsAsync(ct);

        var approved = await db.Referrals.AsNoTracking()
            .Where(r => r.Status == "APPROVED" || r.Status == "REWARDED")
            .OrderByDescending(r => r.EligibleForPaymentAt)
            .ToListAsync(ct);

        var ready = approved.Where(r =>
            r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.Ready).ToList();
        var paid = approved.Where(r => r.Status == "REWARDED").ToList();

        var items = approved
            .Where(r =>
                (r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.Ready) ||
                r.Status == "REWARDED")
            .Select(r => new PaymentInboxItemDto
            {
                Referral = r.ToDto(),
                Amount = r.RewardAmount,
                CanMarkPaid = r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.Ready,
                CanUndoPayment = r.Status == "REWARDED" && r.PaymentStatus == ReferralPaymentStatus.Paid,
            })
            .ToList();

        return new PaymentInboxDto
        {
            ReadyCount = ready.Count,
            PaidCount = paid.Count,
            TotalApprovedCount = approved.Count(r => r.Status == "APPROVED" || r.Status == "REWARDED"),
            Items = items,
        };
    }

    public async Task<PayAllPaymentsResult> PayAllAsync(MarkReferralPaymentRequest body, CancellationToken ct = default)
    {
        await eligibility.ProcessEligibleReferralsAsync(ct);

        var readyIds = await db.Referrals
            .Where(r =>
                r.Status == "APPROVED" &&
                r.PaymentStatus == ReferralPaymentStatus.Ready)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var paid = 0;
        foreach (var id in readyIds)
        {
            try
            {
                await workflow.MarkReferralPaidAsync(
                    id,
                    new MarkReferralPaymentRequest
                    {
                        Paid = true,
                        PaidAt = body.PaidAt,
                        Reference = body.Reference,
                        Actor = body.Actor,
                    },
                    ct);
                paid++;
            }
            catch (InvalidOperationException)
            {
                // skip invalid rows
            }
        }

        return new PayAllPaymentsResult(paid, readyIds.Count);
    }
}
