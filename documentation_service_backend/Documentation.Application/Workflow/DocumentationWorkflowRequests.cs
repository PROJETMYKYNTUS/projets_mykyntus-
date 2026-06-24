using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Application.Workflow;

namespace Documentation.Application.Workflow;

public record ValidateDocumentWorkflowCommand(WorkflowValidateBody Body) : IRequest<WorkflowOperationResult>;

public sealed class ValidateDocumentWorkflowCommandHandler(IDocumentationWorkflowAppService workflow)
    : IRequestHandler<ValidateDocumentWorkflowCommand, WorkflowOperationResult>
{
    public Task<WorkflowOperationResult> Handle(ValidateDocumentWorkflowCommand request, CancellationToken ct) =>
        workflow.ValidateAsync(request.Body.DocumentRequestId, request.Body.Comment, ct);
}

public record ApproveDocumentWorkflowCommand(WorkflowApproveBody Body) : IRequest<WorkflowOperationResult>;

public sealed class ApproveDocumentWorkflowCommandHandler(IDocumentationWorkflowAppService workflow)
    : IRequestHandler<ApproveDocumentWorkflowCommand, WorkflowOperationResult>
{
    public Task<WorkflowOperationResult> Handle(ApproveDocumentWorkflowCommand request, CancellationToken ct) =>
        workflow.ApproveAsync(request.Body.DocumentRequestId, ct);
}

public record RejectDocumentWorkflowCommand(WorkflowRejectBody Body) : IRequest<WorkflowOperationResult>;

public sealed class RejectDocumentWorkflowCommandHandler(IDocumentationWorkflowAppService workflow)
    : IRequestHandler<RejectDocumentWorkflowCommand, WorkflowOperationResult>
{
    public Task<WorkflowOperationResult> Handle(RejectDocumentWorkflowCommand request, CancellationToken ct) =>
        workflow.RejectAsync(request.Body.DocumentRequestId, request.Body.RejectionReason ?? "", ct);
}
