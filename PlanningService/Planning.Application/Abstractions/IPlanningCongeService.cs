using Planning.Application.DTOs;

namespace Planning.Application.Abstractions;

public interface IPlanningCongeService
{
    Task<IReadOnlyList<PlanningCongeListItemDto>> GetBySubServiceAsync(
        int subServiceId,
        string? weekStart,
        CancellationToken ct = default);

    Task<IReadOnlyList<PlanningNewEmployeeDto>> GetNewEmployeesAsync(
        int subServiceId,
        CancellationToken ct = default);

    Task<PlanningCongeListItemDto> CreateAsync(CreateCongeDto dto, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<SetSaturdaySlotResultDto> SetSaturdaySlotAsync(SetSaturdaySlotDto dto, CancellationToken ct = default);

    Task<BulkAbsenceDaysResponseDto> GetBulkAbsenceDaysAsync(
        BulkAbsenceDaysRequestDto request,
        CancellationToken ct = default);
}
