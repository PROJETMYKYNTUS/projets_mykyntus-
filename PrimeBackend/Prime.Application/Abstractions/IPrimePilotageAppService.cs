using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimePilotageAppService
{
    Task<IReadOnlyList<ServicePilotageSummaryDto>> GetCellsSummaryAsync(
        string supervisorUserId,
        string period,
        CancellationToken ct = default);
}
