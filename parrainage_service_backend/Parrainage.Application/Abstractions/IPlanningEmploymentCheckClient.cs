namespace Parrainage.Application.Abstractions;

public sealed class PlanningEmploymentSummary
{
    public bool IsActive { get; set; }
    public bool HasContract { get; set; }
    public string? ContractStatus { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public bool IsEligibleForPaymentConfirmation { get; set; }
    public string? BlockReason { get; set; }
}

public interface IPlanningEmploymentCheckClient
{
    Task<PlanningEmploymentSummary?> GetEmploymentSummaryAsync(string candidateEmployeeId, CancellationToken ct = default);
}
