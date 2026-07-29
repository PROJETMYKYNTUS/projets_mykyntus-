using System.ComponentModel.DataAnnotations;

namespace Auth.Application.DTOs;

public class AdminResetPasswordDto
{
    public Guid? EmployeeId { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public required string NewPassword { get; set; }
}
