namespace Planning.Application.DTOs;

public class ResetPasswordResultDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}
