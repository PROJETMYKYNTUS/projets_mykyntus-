namespace EmployeeDirectory.Application.Exceptions;

public sealed class PilotRotationTenureException : InvalidOperationException
{
    public PilotRotationTenureException(
        string message,
        string? currentServiceId,
        DateTime? currentSince,
        DateTime eligibleAt,
        int daysRemaining)
        : base(message)
    {
        CurrentServiceId = currentServiceId;
        CurrentSince = currentSince;
        EligibleAt = eligibleAt;
        DaysRemaining = daysRemaining;
    }

    public string? CurrentServiceId { get; }
    public DateTime? CurrentSince { get; }
    public DateTime EligibleAt { get; }
    public int DaysRemaining { get; }
}
