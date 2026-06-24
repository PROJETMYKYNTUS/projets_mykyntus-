using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Rules;

public record ListReferralRulesQuery : IRequest<IReadOnlyList<ReferralRuleDto>>;
public sealed class ListReferralRulesQueryHandler(IReferralRulesAppService rules)
    : IRequestHandler<ListReferralRulesQuery, IReadOnlyList<ReferralRuleDto>>
{
    public Task<IReadOnlyList<ReferralRuleDto>> Handle(ListReferralRulesQuery request, CancellationToken ct) =>
        rules.ListAsync(ct);
}

public record GetReferralRulesCatalogQuery : IRequest<IReadOnlyList<ReferralRuleCatalogDto>>;
public sealed class GetReferralRulesCatalogQueryHandler(IReferralRulesAppService rules)
    : IRequestHandler<GetReferralRulesCatalogQuery, IReadOnlyList<ReferralRuleCatalogDto>>
{
    public Task<IReadOnlyList<ReferralRuleCatalogDto>> Handle(GetReferralRulesCatalogQuery request, CancellationToken ct) =>
        rules.GetCatalogAsync(ct);
}

public record UpsertReferralRuleCommand(string Id, UpsertRuleRequest Body) : IRequest<ReferralRuleDto>;
public sealed class UpsertReferralRuleCommandHandler(IReferralRulesAppService rules)
    : IRequestHandler<UpsertReferralRuleCommand, ReferralRuleDto>
{
    public Task<ReferralRuleDto> Handle(UpsertReferralRuleCommand request, CancellationToken ct) =>
        rules.UpsertAsync(request.Id, request.Body, ct);
}

public record DeleteReferralRuleCommand(string Id) : IRequest<bool>;
public sealed class DeleteReferralRuleCommandHandler(IReferralRulesAppService rules)
    : IRequestHandler<DeleteReferralRuleCommand, bool>
{
    public Task<bool> Handle(DeleteReferralRuleCommand request, CancellationToken ct) =>
        rules.DeleteAsync(request.Id, ct);
}
