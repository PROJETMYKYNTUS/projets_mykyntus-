using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using Kyntus.Iam;
using MediatR;

namespace EmployeeDirectory.Application.Iam;

public record GetEffectivePermissionsQuery(Guid SubjectId, string Role) : IRequest<EffectivePermissionsDto>;

public sealed class GetEffectivePermissionsQueryHandler(IIamReadService iam)
    : IRequestHandler<GetEffectivePermissionsQuery, EffectivePermissionsDto>
{
    public Task<EffectivePermissionsDto> Handle(GetEffectivePermissionsQuery request, CancellationToken ct) =>
        iam.GetEffectivePermissionsAsync(request.SubjectId, request.Role, ct);
}

public record EvaluatePolicyCommand(Guid SubjectId, string Role, string Action, string ResourceType, string? ResourceId)
    : IRequest<PolicyDecision>;

public sealed class EvaluatePolicyCommandHandler(IPolicyEvaluator evaluator)
    : IRequestHandler<EvaluatePolicyCommand, PolicyDecision>
{
    public Task<PolicyDecision> Handle(EvaluatePolicyCommand request, CancellationToken ct) =>
        evaluator.EvaluateAsync(
            new PolicyRequest(request.SubjectId, request.Role, request.Action, request.ResourceType, request.ResourceId),
            ct);
}
