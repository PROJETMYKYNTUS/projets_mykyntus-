namespace PlanningService.Models
{
    public class UserSubService
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int SubServiceId { get; set; }
        public SubService SubService { get; set; } = null!;
    }
}
