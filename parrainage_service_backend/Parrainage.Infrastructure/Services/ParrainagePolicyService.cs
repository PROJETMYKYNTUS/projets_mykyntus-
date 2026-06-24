using Kyntus.Iam;
using Parrainage.Application.Authorization;

namespace Parrainage.Infrastructure.Services;

/// <summary>Évaluation IAM unifiée avec repli sur <see cref="ParrainageRoleGuard"/>.</summary>
public sealed class ParrainagePolicyService(IPolicyEvaluator evaluator)
{
    public async Task<bool> CanMarkPaymentAsync(Guid subjectId, string role, CancellationToken ct = default)
    {
        var decision = await evaluator.EvaluateAsync(
            new PolicyRequest(subjectId, role, "parrainage.payment.mark", "ReferralPayment"), ct);
        if (decision.Allowed) return true;
        return ParrainageRoleGuard.CanMarkPayment(role);
    }

    public async Task<bool> IsRhAsync(Guid subjectId, string role, CancellationToken ct = default)
    {
        var decision = await evaluator.EvaluateAsync(
            new PolicyRequest(subjectId, role, "parrainage.referral.manage", "Referral"), ct);
        if (decision.Allowed) return true;
        return ParrainageRoleGuard.IsRh(role);
    }
}
