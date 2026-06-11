namespace Planning.Messaging.Publishers;

/// <summary>
/// Contrat pour publier les événements employé vers RabbitMQ.
/// Injecté dans les Services Planning via DI.
/// </summary>
public interface IEmployePublisher
{
    Task PublishEmployeCreatedAsync(
        Guid employeId,
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom,
        DateTime dateEmbauche,
        bool estMineur,
        CancellationToken ct = default);

    Task PublishEmployeUpdatedAsync(
        Guid employeId,
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom,
        CancellationToken ct = default);

    Task PublishSoldeAnnuelAsync(
        Guid employeId,
        int ancienneteAnnees,
        bool estMineur,
        int annee,
        CancellationToken ct = default);
}