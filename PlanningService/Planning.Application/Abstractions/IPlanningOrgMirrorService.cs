using Planning.Application.DTOs;

namespace Planning.Application.Abstractions;

public interface IPlanningOrgMirrorService
{
    Task<int> SyncFromPrimeTreeAsync(IReadOnlyList<PrimeOrgPoleMirrorDto> poles, CancellationToken ct = default);
    Task<int> SyncFromDirectoryOverviewAsync(string? authorizationHeader, CancellationToken ct = default);
    Task<int> SyncEmployeeSubServicesFromDirectoryOverviewAsync(string? authorizationHeader, CancellationToken ct = default);
    Task<EmployeeImportOrgOverview?> GetDirectoryOverviewAsync(string? authorizationHeader, CancellationToken ct = default);
}
