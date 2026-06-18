namespace PlanningService.DTOs;



public class EmployeeImportFieldConfigDto

{

    public string FieldKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IsRequiredOnCreate { get; set; }

    public List<string> Aliases { get; set; } = new();

    public int SortOrder { get; set; }

}



public class UpdateEmployeeImportConfigRequest

{

    public List<EmployeeImportFieldConfigDto> Fields { get; set; } = new();

}



public class EmployeeImportColumnMappingDto

{

    public int ColumnIndex { get; set; }

    public string SourceHeader { get; set; } = string.Empty;

    public string? SuggestedFieldKey { get; set; }

    public string Confidence { get; set; } = "low";

}



public class EmployeeImportOrgHintDto

{

    public string FieldKey { get; set; } = string.Empty;

    public string SourceValue { get; set; } = string.Empty;

    public string? MatchedValue { get; set; }

    public string Confidence { get; set; } = "low";

    public bool IsNewName { get; set; }

}



public class EmployeeImportResolvedRowDto

{

    public int LineNumber { get; set; }

    public string? Email { get; set; }

    public string? RoleName { get; set; }

    public string RoleConfidence { get; set; } = "low";

    public string? Pole { get; set; }

    public string? Cellule { get; set; }

    public string? Service { get; set; }

    public List<EmployeeImportOrgHintDto> OrgHints { get; set; } = new();

}



public class PendingOrgCreationDto

{

    public string Type { get; set; } = string.Empty;

    public string? Pole { get; set; }

    public string? Cellule { get; set; }

    public string? Service { get; set; }

    public string ConfirmationLabel { get; set; } = string.Empty;

    public List<int> AffectedLineNumbers { get; set; } = new();

    public bool Approved { get; set; } = true;

}



public class EmployeeImportOrgLineIssueDto

{

    public int LineNumber { get; set; }

    public string? Email { get; set; }

    public string Severity { get; set; } = "warning";

    public string Message { get; set; } = string.Empty;

}



public class AcceptedFuzzyOrgMatchDto

{

    public int LineNumber { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string SourceValue { get; set; } = string.Empty;

    public string MatchedValue { get; set; } = string.Empty;

}



public class OrgNodeCreatedReportDto

{

    public string NodeType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Pole { get; set; }

    public string? Cellule { get; set; }

    public int LocalNodeId { get; set; }

    public string? DirectoryNodeId { get; set; }

}



public class EmployeeImportAnalyzeResponse

{

    public Guid ImportSessionId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public List<string> Headers { get; set; } = new();

    public List<EmployeeImportColumnMappingDto> SuggestedMappings { get; set; } = new();

    public List<Dictionary<string, string?>> PreviewRows { get; set; } = new();

    public List<string> Alerts { get; set; } = new();

    public List<EmployeeImportFieldConfigDto> ActiveFields { get; set; } = new();

    public List<PendingOrgCreationDto> PendingOrgCreations { get; set; } = new();

    public List<EmployeeImportResolvedRowDto> ResolvedRows { get; set; } = new();

    public List<EmployeeImportOrgLineIssueDto> OrgLineIssues { get; set; } = new();

}



public class EmployeeImportMappingItemDto

{

    public int ColumnIndex { get; set; }

    public string? FieldKey { get; set; }

}



public class EmployeeImportRevalidateOrgRequest
{
    public Guid ImportSessionId { get; set; }
    public List<EmployeeImportMappingItemDto> Mappings { get; set; } = new();
}

public class EmployeeImportRevalidateOrgResponse
{
    public List<PendingOrgCreationDto> PendingOrgCreations { get; set; } = new();
    public List<EmployeeImportResolvedRowDto> ResolvedRows { get; set; } = new();
    public List<EmployeeImportOrgLineIssueDto> OrgLineIssues { get; set; } = new();
}

public class EmployeeImportExecuteRequest

{

    public Guid ImportSessionId { get; set; }

    public List<EmployeeImportMappingItemDto> Mappings { get; set; } = new();

    public bool ConfirmOrgProvision { get; set; }

    public List<PendingOrgCreationDto> ApprovedOrgCreations { get; set; } = new();

    public List<AcceptedFuzzyOrgMatchDto> AcceptedFuzzyMatches { get; set; } = new();

}



public class EmployeeImportRowResultDto

{

    public int LineNumber { get; set; }

    public string? Email { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Message { get; set; }

}



public class EmployeeImportReportDto

{

    public Guid ImportJobId { get; set; }

    public int TotalLignes { get; set; }

    public int Crees { get; set; }

    public int MisAJour { get; set; }

    public int Ignores { get; set; }

    public int Erreurs { get; set; }

    public DateTime CompletedAt { get; set; }

    public List<EmployeeImportRowResultDto> Lignes { get; set; } = new();

    public List<OrgNodeCreatedReportDto> OrgNodesCreated { get; set; } = new();

}



public class EmployeeImportJobSummaryDto

{

    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int TotalLignes { get; set; }

    public int Crees { get; set; }

    public int MisAJour { get; set; }

    public int Ignores { get; set; }

    public int Erreurs { get; set; }

    public string? StartedByEmail { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

}



public class CreateUserFromImportDto : CreateUserDto

{

    public string? Password { get; set; }

    public bool? IsActiveOnImport { get; set; }

    public Guid? ParentEmployeeId { get; set; }

    public bool? IsNewEmployee { get; set; }

}


