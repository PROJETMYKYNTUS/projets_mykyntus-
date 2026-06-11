namespace Messaging.Contracts;

public record SoldeAnnuelInitialiseMessage
{
    public Guid EmployeId { get; init; }
    public int AncienneteAnnees { get; init; }
    public bool EstMineur { get; init; }
    public int Annee { get; init; }
}