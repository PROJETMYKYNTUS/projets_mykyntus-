using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;
using Planning.Infrastructure.Messaging.Publishers;

namespace Planning.Infrastructure.Messaging.Publishers;

public class EmployePublisher(IOutboxWriter outbox, ILogger<EmployePublisher> logger) : IEmployePublisher
{
    public async Task PublishEmployeCreatedAsync(
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
        CancellationToken ct = default)
    {
        await outbox.EnqueueAsync(new EmployeCreatedMessage
        {
            EmployeId = employeId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            ManagerId = managerId,
            ServiceId = serviceId,
            ServiceNom = serviceNom,
            DateEmbauche = dateEmbauche,
            EstMineur = estMineur,
            Role = role,
            SubServiceId = subServiceId,
            PrimeServiceId = primeServiceId,
            SupervisorId = supervisorId != Guid.Empty ? supervisorId : managerId
        }, aggregateId: employeId.ToString(), ct: ct);

        logger.LogInformation("EmployeCreated enqueued → {EmployeId} rôle={Role}", employeId, role);
    }

    public async Task PublishEmployeUpdatedAsync(
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
        CancellationToken ct = default)
    {
        await outbox.EnqueueAsync(new EmployeUpdatedMessage
        {
            EmployeId = employeId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            ManagerId = managerId,
            ServiceId = serviceId,
            ServiceNom = serviceNom,
            Role = role,
            SubServiceId = subServiceId,
            PrimeServiceId = primeServiceId,
            SupervisorId = supervisorId != Guid.Empty ? supervisorId : managerId,
            SkipOrgStructureFields = skipOrgStructureFields
        }, aggregateId: employeId.ToString(), ct: ct);

        logger.LogInformation("EmployeUpdated enqueued → {EmployeId} rôle={Role}", employeId, role);
    }

    public async Task PublishSoldeAnnuelAsync(
        Guid employeId,
        int ancienneteAnnees,
        bool estMineur,
        int annee,
        CancellationToken ct = default)
    {
        await outbox.EnqueueAsync(new SoldeAnnuelInitialiseMessage
        {
            EmployeId = employeId,
            AncienneteAnnees = ancienneteAnnees,
            EstMineur = estMineur,
            Annee = annee
        }, aggregateId: employeId.ToString(), ct: ct);

        logger.LogInformation("SoldeAnnuel enqueued → {EmployeId} année {Annee}", employeId, annee);
    }
}
