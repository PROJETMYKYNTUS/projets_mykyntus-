namespace Conge.Application.Contracts;

/// <summary>
/// Contrat pour la publication d'événements vers RabbitMQ via MassTransit.
/// Implémenté dans Conge.Infrastructure.
/// </summary>
public interface ICongeEventPublisher
{
    Task PublishCongeValideAsync(
        Guid employeId,
        Guid demandeId,
        DateTime dateDebut,
        DateTime dateFin,
        double nombreJours,
        string typeConge,
        string? typeExceptionnel = null,
        Guid? validateurId = null,
        string? validateurNom = null,
        CancellationToken ct = default);

    Task PublishCongeRefuseAsync(
        Guid employeId,
        Guid demandeId,
        string motif,
        Guid? validateurId = null,
        string? validateurNom = null,
        CancellationToken ct = default);

    Task PublishCongeDemandeAsync(
        Guid employeId,
        Guid demandeId,
        Guid managerId,
        DateTime dateDebut,
        DateTime dateFin,
        CancellationToken ct = default);
}
