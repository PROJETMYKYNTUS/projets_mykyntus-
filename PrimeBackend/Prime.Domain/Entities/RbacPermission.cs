namespace Prime.Domain.Entities;

/// <summary>
/// Permission RBAC (matrice rôle × action × scope) pour le module PRIME.
/// </summary>
public class RbacPermission
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "";
    public string Action { get; set; } = "";
    public string Scope { get; set; } = "Global";
    public bool IsAllowed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
