namespace PlanningService.DTOs
{
    public class CreateCongeDto
    {
        public int UserId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public string AbsenceType { get; set; } = "CongesPayes";
    }

    public record SetSaturdaySlotDto(
        int UserId,
        int Slot  // 1 = Matin (8h-12h) | 2 = Apres-midi (12h-16h)
    );
}
