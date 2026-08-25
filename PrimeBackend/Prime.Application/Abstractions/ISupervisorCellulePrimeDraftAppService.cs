using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface ISupervisorCellulePrimeDraftAppService
{
    Task<SupervisorCellulePrimeDraftResponseDto?> GetAsync(
        string supervisorUserId,
        string? celluleId,
        string? poleId,
        string period,
        string templateId,
        CancellationToken ct = default);

    Task<IReadOnlyList<SupervisorCellulePrimeDraftListItemDto>> ListActiveAsync(
        string supervisorUserId,
        CancellationToken ct = default);

    Task<SupervisorCellulePrimeDraftResponseDto> UpsertAsync(
        UpsertSupervisorCellulePrimeDraftRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftRolloverResultDto> RolloverAsync(
        RolloverCellulePrimeDraftRequest body,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, string supervisorUserId, CancellationToken ct = default);
}
