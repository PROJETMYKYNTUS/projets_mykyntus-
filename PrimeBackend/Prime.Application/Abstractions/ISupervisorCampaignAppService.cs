using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface ISupervisorCampaignAppService
{
    Task<IReadOnlyList<SupervisorCelluleCampaignDto>> GetCampaignAsync(
        string supervisorUserId,
        string period,
        CancellationToken ct = default);
}
