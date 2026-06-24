namespace Prime.Infrastructure.Persistence;

using Prime.Application.Allowance;

public class AllowanceTypeEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string Category { get; set; } = "";
    public string CalculationMode { get; set; } = "Manual";
    public decimal? DefaultAmount { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool RequiresJustification { get; set; }
    public string ApplicableDepartmentKinds { get; set; } = "Support";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<AllowanceTypeDepartmentEntity> DepartmentLinks { get; set; } = new List<AllowanceTypeDepartmentEntity>();
}

public class AllowanceTypeDepartmentEntity
{
    public Guid Id { get; set; }
    public Guid AllowanceTypeId { get; set; }
    public AllowanceTypeEntity AllowanceType { get; set; } = null!;
    public string BusinessDepartmentId { get; set; } = "";
}

public class AllowanceRequestEntity
{
    public Guid Id { get; set; }
    public string EmployeeId { get; set; } = "";
    public string BusinessDepartmentId { get; set; } = "";
    public Guid AllowanceTypeId { get; set; }
    public AllowanceTypeEntity AllowanceType { get; set; } = null!;
    public string Period { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MAD";
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "Manual";
    public string Status { get; set; } = AllowanceRequestStatuses.Draft;
    public string CreatedByUserId { get; set; } = "";
    public string? RejectionReason { get; set; }
    public string? ManagerApprovedByUserId { get; set; }
    public DateTimeOffset? ManagerApprovedAt { get; set; }
    public string? RhApprovedByUserId { get; set; }
    public DateTimeOffset? RhApprovedAt { get; set; }
    public string? ComptaApprovedByUserId { get; set; }
    public DateTimeOffset? ComptaApprovedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<AllowanceRequestHistoryEntity> History { get; set; } = new List<AllowanceRequestHistoryEntity>();
}

public class AllowanceRequestHistoryEntity
{
    public Guid Id { get; set; }
    public Guid AllowanceRequestId { get; set; }
    public AllowanceRequestEntity AllowanceRequest { get; set; } = null!;
    public string Action { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string? Comment { get; set; }
    public DateTimeOffset At { get; set; }
}

public class AllowanceWorkflowStepEntity
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string ApproverRole { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AllowanceRuleEntity
{
    public Guid Id { get; set; }
    public Guid AllowanceTypeId { get; set; }
    public AllowanceTypeEntity AllowanceType { get; set; } = null!;
    public string BusinessDepartmentId { get; set; } = "";
    public string ConditionJson { get; set; } = "{}";
    public string FormulaJson { get; set; } = "{}";
    public string DataSource { get; set; } = "Manual";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Marquage explicite « aucune prime ce mois » pour un collaborateur × période.</summary>
public class AllowanceNoBonusMarkerEntity
{
    public Guid Id { get; set; }
    public string EmployeeId { get; set; } = "";
    public string BusinessDepartmentId { get; set; } = "";
    public string Period { get; set; } = "";
    public string MarkedByUserId { get; set; } = "";
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
