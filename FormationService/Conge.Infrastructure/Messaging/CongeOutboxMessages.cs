// Namespace propre à Conge
namespace Conge.Infrastructure.Messaging.Messages;

public record CongeValideMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
    public double NombreJours { get; init; }
    public DateTime DateValidation { get; init; }
}

public record CongeRefuseMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public string Motif { get; init; } = string.Empty;
}

public record CongeDemandeMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public Guid ManagerId { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
}