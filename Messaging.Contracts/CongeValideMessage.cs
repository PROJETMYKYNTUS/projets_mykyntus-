namespace Messaging.Contracts;

public record CongeValideMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
    public double NombreJours { get; init; }
    public DateTime DateValidation { get; init; }
}