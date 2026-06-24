using Documentation.Application.Api;

namespace Documentation.Application.Workflow;

public sealed class WorkflowValidateBody
{
    public Guid DocumentRequestId { get; set; }
    public string? Comment { get; set; }
}

public sealed class WorkflowApproveBody
{
    public Guid DocumentRequestId { get; set; }
}

public sealed class WorkflowRejectBody
{
    public Guid DocumentRequestId { get; set; }
    public string? RejectionReason { get; set; }
}
