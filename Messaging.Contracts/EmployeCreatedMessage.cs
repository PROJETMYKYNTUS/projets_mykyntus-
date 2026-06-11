namespace Messaging.Contracts;

public record EmployeCreatedMessage
{
    public Guid EmployeId { get; init; }
    public string Nom { get; init; } = string.Empty;
    public string Prenom { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }        // ✅ Guid (non-nullable)
    public Guid ServiceId { get; init; }
    public string ServiceNom { get; init; } = string.Empty;
    public DateTime DateEmbauche { get; init; }
    public bool EstMineur { get; init; }
}