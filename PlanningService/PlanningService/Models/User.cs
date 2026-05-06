namespace PlanningService.Models;

using System.ComponentModel.DataAnnotations;

public class User
{
    public int Id { get; set; }

    // FK Role
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // FK SubService (nullable pour RH)
    public int? SubServiceId { get; set; }
    public SubService? SubService { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public int EarlyShiftCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public bool IsNewEmployee { get; set; } = false;
    public int Level { get; set; } = 1;
    public ICollection<UserSubService> ManagedSubServices { get; set; } = new List<UserSubService>();

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<Declaration> Declarations { get; set; } = new List<Declaration>();
}