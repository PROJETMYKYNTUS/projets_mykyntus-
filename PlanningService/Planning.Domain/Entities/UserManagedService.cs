namespace Planning.Domain.Entities;

/// <summary>DEPRECATED : préférer les affectations Prime (Organisation RH).</summary>
[Obsolete("Utiliser les affectations Prime (Organisation RH).")]
public class UserManagedService
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
}