using MediatR;
using Microsoft.AspNetCore.Http;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Referrals;

public record ListReferralsQuery : IRequest<IReadOnlyList<ReferralDto>>;
public sealed class ListReferralsQueryHandler(IReferralAppService referrals)
    : IRequestHandler<ListReferralsQuery, IReadOnlyList<ReferralDto>>
{
    public Task<IReadOnlyList<ReferralDto>> Handle(ListReferralsQuery request, CancellationToken ct) =>
        referrals.ListAsync(ct);
}

public record GetReferralHistoryQuery : IRequest<IReadOnlyList<ReferralHistoryDto>>;
public sealed class GetReferralHistoryQueryHandler(IReferralAppService referrals)
    : IRequestHandler<GetReferralHistoryQuery, IReadOnlyList<ReferralHistoryDto>>
{
    public Task<IReadOnlyList<ReferralHistoryDto>> Handle(GetReferralHistoryQuery request, CancellationToken ct) =>
        referrals.GetHistoryAsync(ct);
}

public record GetReferralByIdQuery(string Id) : IRequest<ReferralDto?>;
public sealed class GetReferralByIdQueryHandler(IReferralAppService referrals)
    : IRequestHandler<GetReferralByIdQuery, ReferralDto?>
{
    public Task<ReferralDto?> Handle(GetReferralByIdQuery request, CancellationToken ct) =>
        referrals.GetByIdAsync(request.Id, ct);
}

public record GetReferralRewardPreviewQuery(string Id) : IRequest<ReferralRewardPreviewDto?>;
public sealed class GetReferralRewardPreviewQueryHandler(IReferralAppService referrals)
    : IRequestHandler<GetReferralRewardPreviewQuery, ReferralRewardPreviewDto?>
{
    public Task<ReferralRewardPreviewDto?> Handle(GetReferralRewardPreviewQuery request, CancellationToken ct) =>
        referrals.GetRewardPreviewAsync(request.Id, ct);
}

public record CreateReferralCommand(CreateReferralRequest Body) : IRequest<ReferralDto>;
public sealed class CreateReferralCommandHandler(IReferralAppService referrals)
    : IRequestHandler<CreateReferralCommand, ReferralDto>
{
    public Task<ReferralDto> Handle(CreateReferralCommand request, CancellationToken ct) =>
        referrals.CreateAsync(request.Body, ct);
}

public record UpdateReferralCommand(string Id, UpdateReferralRequest Body) : IRequest<ReferralDto?>;
public sealed class UpdateReferralCommandHandler(IReferralAppService referrals)
    : IRequestHandler<UpdateReferralCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(UpdateReferralCommand request, CancellationToken ct) =>
        referrals.UpdateAsync(request.Id, request.Body, ct);
}

public record ProcessReferralCommand(string Id, ProcessReferralRequest Body) : IRequest<ReferralDto?>;
public sealed class ProcessReferralCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ProcessReferralCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ProcessReferralCommand request, CancellationToken ct) =>
        referrals.ProcessAsync(request.Id, request.Body, ct);
}

public record ApproveReferralCommand(string Id, ApproveReferralRequest Body) : IRequest<ReferralDto?>;
public sealed class ApproveReferralCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ApproveReferralCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ApproveReferralCommand request, CancellationToken ct) =>
        referrals.ApproveAsync(request.Id, request.Body, ct);
}

public record ConfirmProductionCommand(string Id, ConfirmProductionStartRequest Body) : IRequest<ReferralDto?>;
public sealed class ConfirmProductionCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ConfirmProductionCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ConfirmProductionCommand request, CancellationToken ct) =>
        referrals.ConfirmProductionAsync(request.Id, request.Body, ct);
}

public record RejectEarlyDepartureCommand(string Id, RejectEarlyDepartureRequest Body) : IRequest<ReferralDto?>;
public sealed class RejectEarlyDepartureCommandHandler(IReferralAppService referrals)
    : IRequestHandler<RejectEarlyDepartureCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(RejectEarlyDepartureCommand request, CancellationToken ct) =>
        referrals.RejectEarlyDepartureAsync(request.Id, request.Body, ct);
}

public record ExtendTrainingCommand(string Id, ExtendTrainingRequest Body) : IRequest<ReferralDto?>;
public sealed class ExtendTrainingCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ExtendTrainingCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ExtendTrainingCommand request, CancellationToken ct) =>
        referrals.ExtendTrainingAsync(request.Id, request.Body, ct);
}

public record ConfirmEligibilityCommand(string Id, ConfirmPaymentEligibilityRequest Body) : IRequest<ReferralDto?>;
public sealed class ConfirmEligibilityCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ConfirmEligibilityCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ConfirmEligibilityCommand request, CancellationToken ct) =>
        referrals.ConfirmEligibilityAsync(request.Id, request.Body, ct);
}

public record ChangeReferralStatusCommand(string Id, UpdateStatusRequest Body) : IRequest<ReferralDto?>;
public sealed class ChangeReferralStatusCommandHandler(IReferralAppService referrals)
    : IRequestHandler<ChangeReferralStatusCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(ChangeReferralStatusCommand request, CancellationToken ct) =>
        referrals.ChangeStatusAsync(request.Id, request.Body, ct);
}

public record RewardReferralCommand(string Id, RewardRequest Body, ParrainageResolvedUser User) : IRequest<ReferralDto?>;
public sealed class RewardReferralCommandHandler(IReferralAppService referrals)
    : IRequestHandler<RewardReferralCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(RewardReferralCommand request, CancellationToken ct) =>
        referrals.RewardAsync(request.Id, request.Body, request.User, ct);
}

public record MarkReferralPaymentCommand(string Id, MarkReferralPaymentRequest Body) : IRequest<ReferralDto?>;
public sealed class MarkReferralPaymentCommandHandler(IReferralAppService referrals)
    : IRequestHandler<MarkReferralPaymentCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(MarkReferralPaymentCommand request, CancellationToken ct) =>
        referrals.MarkPaymentAsync(request.Id, request.Body, ct);
}

public record UploadReferralCvCommand(string Id, IFormFile File) : IRequest<ReferralDto?>;
public sealed class UploadReferralCvCommandHandler(IReferralAppService referrals)
    : IRequestHandler<UploadReferralCvCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(UploadReferralCvCommand request, CancellationToken ct) =>
        referrals.UploadCvAsync(request.Id, request.File, ct);
}

public record OpenReferralCvQuery(string Id) : IRequest<ReferralCvFile?>;
public sealed class OpenReferralCvQueryHandler(IReferralAppService referrals)
    : IRequestHandler<OpenReferralCvQuery, ReferralCvFile?>
{
    public Task<ReferralCvFile?> Handle(OpenReferralCvQuery request, CancellationToken ct) =>
        referrals.OpenCvAsync(request.Id, ct);
}

public record ListOnboardingReferralsQuery : IRequest<IReadOnlyList<ReferralDto>>;
public sealed class ListOnboardingReferralsQueryHandler(IReferralAppService referrals)
    : IRequestHandler<ListOnboardingReferralsQuery, IReadOnlyList<ReferralDto>>
{
    public Task<IReadOnlyList<ReferralDto>> Handle(ListOnboardingReferralsQuery request, CancellationToken ct) =>
        referrals.ListOnboardingAsync(ct);
}

public record LinkEmployeeCommand(string Id, LinkEmployeeRequest Body) : IRequest<ReferralDto?>;
public sealed class LinkEmployeeCommandHandler(IReferralAppService referrals)
    : IRequestHandler<LinkEmployeeCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(LinkEmployeeCommand request, CancellationToken ct) =>
        referrals.LinkEmployeeAsync(request.Id, request.Body, ct);
}

public record CompleteOnboardingCommand(string Id, CompleteOnboardingRequest Body) : IRequest<ReferralDto?>;
public sealed class CompleteOnboardingCommandHandler(IReferralAppService referrals)
    : IRequestHandler<CompleteOnboardingCommand, ReferralDto?>
{
    public Task<ReferralDto?> Handle(CompleteOnboardingCommand request, CancellationToken ct) =>
        referrals.CompleteOnboardingAsync(request.Id, request.Body, ct);
}
