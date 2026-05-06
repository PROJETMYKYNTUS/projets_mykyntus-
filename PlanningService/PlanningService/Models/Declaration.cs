using PlanningService.Enums;
using System.ComponentModel.DataAnnotations;
namespace PlanningService.Models;

public class Declaration
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? ResolverId { get; set; }   // FK explicite
    public User? Resolver { get; set; }
    public int ShiftAssignmentId { get; set; }
    public ShiftAssignment ShiftAssignment { get; set; }

    [Required]
    public string Reason { get; set; }

    public DeclarationStatus Status { get; set; } = DeclarationStatus.Pending;


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
