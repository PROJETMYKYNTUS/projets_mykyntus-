using MassTransit;
using Planning.Messaging.Messages;
using Planning.Messaging.Publishers;
using PlanningService.Interfaces;


namespace PlanningService.Messaging.Publishers;

public class EmployePublisher : IEmployePublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<EmployePublisher> _logger;

    public EmployePublisher(IPublishEndpoint publishEndpoint, ILogger<EmployePublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

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
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new EmployeCreatedMessage
        {
            EmployeId = employeId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            ManagerId = managerId,
            ServiceId = serviceId,
            ServiceNom = serviceNom,
            DateEmbauche = dateEmbauche,
            EstMineur = estMineur
        }, ct);

        _logger.LogInformation("📤 EmployeCreated publié → {EmployeId}", employeId);
    }

    public async Task PublishEmployeUpdatedAsync(
        Guid employeId,
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom,
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new EmployeUpdatedMessage
        {
            EmployeId = employeId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            ManagerId = managerId,
            ServiceId = serviceId,
            ServiceNom = serviceNom
        }, ct);

        _logger.LogInformation("📤 EmployeUpdated publié → {EmployeId}", employeId);
    }

    public async Task PublishSoldeAnnuelAsync(
        Guid employeId,
        int ancienneteAnnees,
        bool estMineur,
        int annee,
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new SoldeAnnuelInitialiseMessage
        {
            EmployeId = employeId,
            AncienneteAnnees = ancienneteAnnees,
            EstMineur = estMineur,
            Annee = annee
        }, ct);

        _logger.LogInformation("📤 SoldeAnnuel publié → {EmployeId} année {Annee}", employeId, annee);
    }
}
