namespace PlanningService.Models;

public class EmployeeImportFieldConfig
{
    public int Id { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsRequiredOnCreate { get; set; }
    public string AliasesJson { get; set; } = "[]";
    public int SortOrder { get; set; }
}

public class EmployeeImportJob
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
    public ICollection<EmployeeImportJobLine> Lines { get; set; } = new List<EmployeeImportJobLine>();
}

public class EmployeeImportJobLine
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public EmployeeImportJob Job { get; set; } = null!;
    public int LineNumber { get; set; }
    public string? Email { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class EmployeeImportSession
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string HeadersJson { get; set; } = "[]";
    public string RowsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
