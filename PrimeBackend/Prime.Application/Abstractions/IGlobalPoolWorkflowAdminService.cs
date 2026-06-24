using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IGlobalPoolWorkflowAdminService
{
    Task<IReadOnlyList<GlobalPoolWorkflowStepDto>> ListStepsAsync(CancellationToken ct = default);
    Task<GlobalPoolWorkflowStepDto> CreateStepAsync(UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct = default);
    Task<GlobalPoolWorkflowStepDto?> UpdateStepAsync(Guid id, UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct = default);
    Task<bool> DeleteStepAsync(Guid id, CancellationToken ct = default);
}
