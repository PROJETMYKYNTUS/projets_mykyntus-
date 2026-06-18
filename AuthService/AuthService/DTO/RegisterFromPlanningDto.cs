namespace AuthService.DTO;

public class RegisterFromPlanningDto
{
    public string Email { get; set; } = string.Empty;
    public string DefaultPassword { get; set; } = "Azerty@123";
    public int RoleId { get; set; }
    public string? RoleName { get; set; }
}

public class RegisterFromPlanningResponseDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
}