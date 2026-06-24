using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Payments;

public record GetPaymentInboxQuery : IRequest<PaymentInboxDto>;
public sealed class GetPaymentInboxQueryHandler(IPaymentAppService payments)
    : IRequestHandler<GetPaymentInboxQuery, PaymentInboxDto>
{
    public Task<PaymentInboxDto> Handle(GetPaymentInboxQuery request, CancellationToken ct) =>
        payments.GetInboxAsync(ct);
}

public record PayAllReferralsCommand(MarkReferralPaymentRequest Body) : IRequest<PayAllPaymentsResult>;
public sealed class PayAllReferralsCommandHandler(IPaymentAppService payments)
    : IRequestHandler<PayAllReferralsCommand, PayAllPaymentsResult>
{
    public Task<PayAllPaymentsResult> Handle(PayAllReferralsCommand request, CancellationToken ct) =>
        payments.PayAllAsync(request.Body, ct);
}
