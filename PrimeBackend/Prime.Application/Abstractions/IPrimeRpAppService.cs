using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.Abstractions;

public interface IPrimeRpAppService
{
    Task<List<string>> GetAssignedProjectIdsAsync(string rpUserId, CancellationToken ct = default);
    Task<ChefProjetDashboardStats> GetDashboardStatsAsync(string rpUserId, CancellationToken ct = default);
    Task<List<ChefProjetTeamMemberPerformance>> GetTeamPerformanceByProjectAsync(string rpUserId, CancellationToken ct = default);
    Task<List<ChefProjetValidationItem>> GetSuperviseurValidatedPrimesAsync(string rpUserId, CancellationToken ct = default);
    Task<ChefProjetValidationItem> UpdateValidationStatusAsync(string ficheId, string status, string rpUserId, CancellationToken ct = default);
}
