namespace Planning.Domain.Enums;

public enum PlanningReinforcementRequestStatus
{
    Open = 0,
    Filled = 1,
    Cancelled = 2,
}

public enum PlanningReinforcementVolunteerStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Selected = 3,
    Rejected = 4,
}
