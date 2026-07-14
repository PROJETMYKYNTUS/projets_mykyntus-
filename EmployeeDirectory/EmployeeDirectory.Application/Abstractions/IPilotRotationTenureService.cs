using EmployeeDirectory.Application.Dtos;

namespace EmployeeDirectory.Application.Abstractions;

public interface IPilotRotationTenureService
{
    Task BootstrapProjectedPilotsAsync(CancellationToken ct = default);

    Task<PilotRotationEligibilityDto> GetEligibilityAsync(
        Guid employeeId,
        string targetServiceId,
        CancellationToken ct = default);

    Task ValidateRotationAsync(
        Guid employeeId,
        string targetServiceId,
        bool forceTenureOverride,
        string? reason,
        CancellationToken ct = default);

    Task<IReadOnlyList<PilotRotationHistoryEntryDto>> GetRotationHistoryAsync(
        Guid employeeId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PilotRotationSummaryDto>> ListRotationSummariesAsync(
        string? serviceId,
        DateTime? from,
        DateTime? to,
        int? minRotations,
        int? maxRotations,
        string? sort,
        CancellationToken ct = default);

    Task ApplyRotationHrProfileAsync(
        Guid employeeId,
        string previousServiceId,
        CancellationToken ct = default);
}
