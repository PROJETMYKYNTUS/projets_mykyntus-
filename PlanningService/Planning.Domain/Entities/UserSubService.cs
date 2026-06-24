namespace Planning.Domain.Entities
{
    /// <summary>DEPRECATED : préférer les affectations Prime (Organisation RH).</summary>
    [Obsolete("Utiliser les affectations Prime (Organisation RH).")]
    public class UserSubService
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int SubServiceId { get; set; }
        public SubService SubService { get; set; } = null!;
    }
}
