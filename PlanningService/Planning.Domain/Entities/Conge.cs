using Planning.Domain.Enums;

namespace Planning.Domain.Entities;

public class Conge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public CongeStatus Status { get; set; } = CongeStatus.Approved;
    public AbsenceType AbsenceType { get; set; } = AbsenceType.CongesPayes;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Id de la demande Conge.API (sync validate/refuse/cancel).</summary>
    public Guid? SourceDemandeId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}