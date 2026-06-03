using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Models;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/payments")]
public sealed class PaymentsController(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    IParrainageRequestUserResolver userResolver) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<ActionResult<PaymentInboxDto>> Inbox(CancellationToken ct)
    {
        var user = userResolver.Resolve(Request);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role) && user.Role != "ADMIN")
            return Forbid();

        await ProcessEligibilityAsync(ct);

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

        return Ok(new PaymentInboxDto
        {
            ReadyCount = ready.Count,
            PaidCount = paid.Count,
            TotalApprovedCount = approved.Count(r => r.Status == "APPROVED" || r.Status == "REWARDED"),
            Items = items,
        });
    }

    [HttpPost("pay-all")]
    public async Task<ActionResult<object>> PayAll([FromBody] MarkReferralPaymentRequest body, CancellationToken ct)
    {
        var user = userResolver.Resolve(Request);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))
            return Forbid();

        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };

        await ProcessEligibilityAsync(ct);

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
                // skip
            }
        }

        return Ok(new { paid, total = readyIds.Count });
    }

    private async Task ProcessEligibilityAsync(CancellationToken ct)
    {
        var eligibility = HttpContext.RequestServices.GetRequiredService<ReferralEligibilityService>();
        await eligibility.ProcessEligibleReferralsAsync(ct);
    }
}
