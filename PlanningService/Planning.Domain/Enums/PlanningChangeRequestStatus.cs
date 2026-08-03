namespace Planning.Domain.Enums;

public enum PlanningChangeRequestStatus
{
    /// <summary>En attente d'acceptation du collègue (anciennement Pending).</summary>
    PendingPartner = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
    /// <summary>Collègue a accepté — en attente de validation superviseur.</summary>
    PendingSupervisor = 4,
}
