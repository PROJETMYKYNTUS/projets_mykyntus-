using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public sealed record WorkflowOperationResult(DocumentRequestResponse? Response, int StatusCode, string? Error);

public interface IDocumentationWorkflowAppService
{
    Task<WorkflowOperationResult> ValidateAsync(Guid documentRequestId, string? comment, CancellationToken ct = default);
    Task<WorkflowOperationResult> ApproveAsync(Guid documentRequestId, CancellationToken ct = default);
    Task<WorkflowOperationResult> RejectAsync(Guid documentRequestId, string rejectionReason, CancellationToken ct = default);
}
