using Planning.Application.DTOs;

namespace Planning.Application.Abstractions;

public interface IOrgReconciliationService
{
    Task<int> BackfillFromPrimeAsync(PrimeOrgBackfillRequest request, CancellationToken ct = default);
    Task<OrgReconciliationVerifyDto> SyncFromPrimeAsync(CancellationToken ct = default);
    Task<OrgReconciliationVerifyDto> SyncFromDirectoryAsync(string? authorizationHeader, CancellationToken ct = default);
    Task<OrgReconciliationVerifyDto> VerifyAsync(CancellationToken ct = default);
}
