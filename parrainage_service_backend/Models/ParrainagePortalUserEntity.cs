namespace ParrainageBackend.Models;

/// <summary>Utilisateur portail parrainage (liaison JWT e-mail Auth).</summary>
public sealed class ParrainagePortalUserEntity
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "PILOTE";
    public string? ProjectId { get; set; }
    public string? ParentId { get; set; }
}
