using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IPaymentAppService
{
    Task<PaymentInboxDto> GetInboxAsync(CancellationToken ct = default);
    Task<PayAllPaymentsResult> PayAllAsync(MarkReferralPaymentRequest body, CancellationToken ct = default);
}

public sealed record PayAllPaymentsResult(int Paid, int Total);
