namespace AuthService.DTO;

public sealed class AuthMeDto
{
    public Guid SubjectId { get; set; }
    public int AuthUserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string TenantId { get; set; } = "atlas-tech-demo";
}
