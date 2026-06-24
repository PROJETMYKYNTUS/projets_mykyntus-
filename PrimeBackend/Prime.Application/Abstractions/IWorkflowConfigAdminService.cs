using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IWorkflowConfigAdminService
{
    Task<IReadOnlyList<WorkflowStepConfigDto>> ListStepsAsync(CancellationToken ct = default);
    Task<WorkflowStepConfigDto> CreateStepAsync(UpsertWorkflowStepConfigRequest body, CancellationToken ct = default);
    Task<WorkflowStepConfigDto?> UpdateStepAsync(Guid id, UpsertWorkflowStepConfigRequest body, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowStepConfigDto>> RechainAllStepsAsync(CancellationToken ct = default);
    Task<bool> DeleteStepAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowGlobalConfigDto> GetGlobalAsync(CancellationToken ct = default);
    Task<WorkflowGlobalConfigDto> UpdateGlobalAsync(UpdateWorkflowGlobalConfigRequest body, CancellationToken ct = default);
}
