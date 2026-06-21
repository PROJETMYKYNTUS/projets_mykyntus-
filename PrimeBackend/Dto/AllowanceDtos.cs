namespace PrimeBackend.Dto;

public record AllowanceTypeDto(
    Guid Id,
    string Code,
    string Label,
    string Category,
    string CalculationMode,
    decimal? DefaultAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    bool RequiresJustification,
    string ApplicableDepartmentKinds,
    bool IsActive);

public record CreateAllowanceTypeRequest(
    string Code,
    string Label,
    string Category,
    string? CalculationMode,
    decimal? DefaultAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    bool RequiresJustification,
    string? ApplicableDepartmentKinds);

public record AllowanceRequestDto(
    Guid Id,
    string EmployeeId,
    string BusinessDepartmentId,
    Guid AllowanceTypeId,
    string TypeCode,
    string TypeLabel,
    string Period,
    decimal Amount,
    string Currency,
    string Reason,
    string Source,
    string Status,
    string CreatedByUserId,
    string? RejectionReason,
    DateTimeOffset? ManagerApprovedAt,
    DateTimeOffset? RhApprovedAt,
    DateTimeOffset? ComptaApprovedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);

public class CreateAllowanceRequestBody
{
    public string EmployeeId { get; set; } = "";
    public Guid AllowanceTypeId { get; set; }
    public string Period { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Reason { get; set; }
    public string? Source { get; set; }
}

public record RejectAllowanceBody(string Reason);

public class UpdateAllowanceRequestBody
{
    public Guid? AllowanceTypeId { get; set; }
    public string? Period { get; set; }
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}

public record BusinessDepartmentMirrorDto(
    string Id,
    string Code,
    string Name,
    string Kind,
    string? ManagerEmployeeId,
    bool IsActive,
    IReadOnlyList<string> PoleIds);
