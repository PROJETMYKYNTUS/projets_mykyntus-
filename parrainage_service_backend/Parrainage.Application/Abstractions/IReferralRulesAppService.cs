using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IReferralRulesAppService
{
    Task<IReadOnlyList<ReferralRuleDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReferralRuleCatalogDto>> GetCatalogAsync(CancellationToken ct = default);
    Task<ReferralRuleDto> UpsertAsync(string id, UpsertRuleRequest body, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
