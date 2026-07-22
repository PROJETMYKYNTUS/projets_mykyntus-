namespace Planning.Domain.Entities;

using System.ComponentModel.DataAnnotations;

public class User
{
    public int Id { get; set; }
    public Guid Guid { get; set; } = Guid.NewGuid();

    // ?? AJOUTER JUSTE CETTE LIGNE (nullable au début)
    public int? AuthUserId { get; set; }  // ? null pour anciens users, rempli pour nouveaux

    // ? TOUT LE RESTE RESTE INTACT
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int? SubServiceId { get; set; }
    public SubService? SubService { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int EarlyShiftCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public bool IsNewEmployee { get; set; } = false;
    public int Level { get; set; } = 1;
    /// <summary>Miroir Directory / HTEL.</summary>
    public int? IdTechnicien { get; set; }
    public string? HtelCode { get; set; }
    public ICollection<UserSubService> ManagedSubServices { get; set; } = new List<UserSubService>();
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<Declaration> Declarations { get; set; } = new List<Declaration>();
  
    public ICollection<UserManagedService> ManagedServices { get; set; } = new List<UserManagedService>();
}