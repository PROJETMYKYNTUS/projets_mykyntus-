using Conge.Application.Contracts;
using MassTransit;

namespace Conge.Infrastructure.Messaging.Publishers;

public class CongeEventPublisher : ICongeEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public CongeEventPublisher(IPublishEndpoint publishEndpoint)
        => _publishEndpoint = publishEndpoint;

    public async Task PublishCongeValideAsync(
        Guid employeId,
        Guid demandeId,
        DateTime dateDebut,
        DateTime dateFin,
        double nombreJours,
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new CongeValideMessage
        {
            DemandeId = demandeId,
            EmployeId = employeId,
            DateDebut = dateDebut,
            DateFin = dateFin,
            NombreJours = nombreJours,
            DateValidation = DateTime.UtcNow
        }, ct);
    }

    public async Task PublishCongeRefuseAsync(
        Guid employeId,
        Guid demandeId,
        string motif,
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new CongeRefuseMessage
        {
            DemandeId = demandeId,
            EmployeId = employeId,
            Motif = motif
        }, ct);
    }

    public async Task PublishCongeDemandeAsync(
        Guid employeId,
        Guid demandeId,
        Guid managerId,
        DateTime dateDebut,
        DateTime dateFin,
        CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(new CongeDemandeMessage
        {
            DemandeId = demandeId,
            EmployeId = employeId,
            ManagerId = managerId,
            DateDebut = dateDebut,
            DateFin = dateFin
        }, ct);
    }
}