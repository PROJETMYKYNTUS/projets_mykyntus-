using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Domain.Entities;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class PaymentAppService(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    ReferralEligibilityService eligibility,
    IPlanningEmploymentCheckClient employmentCheck) : IPaymentAppService
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

        var items = new List<PaymentInboxItemDto>();
        foreach (var r in approved.Where(r =>
                     (r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.Ready) ||
                     r.Status == "REWARDED"))
        {
            var dto = r.ToDto();
            var canMarkPaid = r.Status == "APPROVED" && r.PaymentStatus == ReferralPaymentStatus.Ready;
            if (!string.IsNullOrWhiteSpace(r.CandidateEmployeeId))
            {
                var summary = await employmentCheck.GetEmploymentSummaryAsync(r.CandidateEmployeeId, ct);
                if (summary is not null)
                {
                    dto.EmploymentCheckSummary = new EmploymentCheckSummaryDto
                    {
                        IsActive = summary.IsActive,
                        ContractStatus = summary.ContractStatus,
                        ProbationEndDate = summary.ProbationEndDate,
                        IsEligibleForPaymentConfirmation = summary.IsEligibleForPaymentConfirmation,
                        BlockReason = summary.BlockReason,
                    };
                    if (!summary.IsEligibleForPaymentConfirmation)
                        canMarkPaid = false;
                }
            }

            items.Add(new PaymentInboxItemDto
            {
                Referral = dto,
                Amount = r.RewardAmount,
                CanMarkPaid = canMarkPaid,
                CanUndoPayment = r.Status == "REWARDED" && r.PaymentStatus == ReferralPaymentStatus.Paid,
            });
        }

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
