namespace Documentation.Application.Api;

public enum DocumentRequestListScope
{
    AllVisible,
    MyRequests,
    AssignedToMe,
}

public sealed class DocumentRequestListQuery
{
    public DocumentRequestListScope Scope { get; init; } = DocumentRequestListScope.AllVisible;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public string? Type { get; init; }
    public string? Role { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public sealed class CreateDocumentRequestBody
{
    public Guid? RequesterUserId { get; set; }
    public Guid? BeneficiaryUserId { get; set; }
    public string? DocumentTypeId { get; set; }
    public bool IsCustomType { get; set; }
    public string? CustomTypeDescription { get; set; }
    public string? Reason { get; set; }
    public string? ComplementaryComments { get; set; }
    public string? DocumentTemplateId { get; set; }
    public Dictionary<string, string>? InitialFieldValues { get; set; }
}

public sealed class WorkflowValidatePutBody
{
    public string? Comment { get; set; }
}

public sealed class WorkflowRejectPutBody
{
    public string? RejectionReason { get; set; }
}
