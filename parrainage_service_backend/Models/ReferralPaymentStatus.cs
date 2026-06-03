namespace ParrainageBackend.Models;

public static class ReferralPaymentStatus
{
    public const string NotEligible = "NOT_ELIGIBLE";
    /// <summary>Période minimum écoulée — la RH doit confirmer que le candidat est toujours en poste.</summary>
    public const string AwaitingRh = "AWAITING_RH";
    public const string Ready = "READY";
    public const string Paid = "PAID";
}
