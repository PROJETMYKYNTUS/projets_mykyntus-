namespace Planning.Messaging.Publishers;

/// <summary>
/// Contrat pour publier les événements employé vers RabbitMQ.
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
        string role,
        int? subServiceId = null,
        string? primeServiceId = null,
        Guid supervisorId = default,
        CancellationToken ct = default);

    Task PublishEmployeUpdatedAsync(
        Guid employeId,
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom,
        string role,
        int? subServiceId = null,
        string? primeServiceId = null,
        Guid supervisorId = default,
        bool skipOrgStructureFields = false,
        CancellationToken ct = default);

    Task PublishSoldeAnnuelAsync(
        Guid employeId,
        int ancienneteAnnees,
        bool estMineur,
        int annee,
        CancellationToken ct = default);
}
