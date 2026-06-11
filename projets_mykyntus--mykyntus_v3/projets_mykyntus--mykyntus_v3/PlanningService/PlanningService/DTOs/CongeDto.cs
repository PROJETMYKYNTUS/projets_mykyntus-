namespace PlanningService.DTOs
{
    public record CreateCongeDto(
    int UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string AbsenceType
);

    public record SetSaturdaySlotDto(
        int UserId,
        int Slot  // 1 = Matin (8h-12h) | 2 = Apres-midi (12h-16h)
    );
}
