using Conge.Application.Commands.InitialiserSolde;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Persistence.Repositories;
using MassTransit;
using MediatR;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consomme l'event EmployeCreated depuis le service RH.
/// Crée le snapshot et initialise le solde de congé.
/// </summary>
public class EmployeCreatedConsumer : IConsumer<EmployeCreatedMessage>
{
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public EmployeCreatedConsumer(
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<EmployeCreatedMessage> context)
    {
        var msg = context.Message;

        // Idempotence : vérifier si le snapshot existe déjà
        var exists = await _employeRepo.ExistsAsync(msg.EmployeId, context.CancellationToken);
        if (exists) return;

        // Créer le snapshot
        var snapshot = EmployeSnapshot.Creer(
            msg.EmployeId,
            msg.Nom,
            msg.Prenom,
            msg.Email,
            msg.ManagerId,
            msg.ServiceId,
            msg.ServiceNom,
            msg.DateEmbauche,
            msg.EstMineur);

        await _employeRepo.AddAsync(snapshot, context.CancellationToken);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);

        // Initialiser le solde de l'année courante via MediatR
        var anciennete = snapshot.GetAncienneteAnnees();
        await _mediator.Send(new InitialiserSoldeCommand(
            msg.EmployeId,
            anciennete,
            msg.EstMineur,
            DateTime.Today.Year), context.CancellationToken);
    }
}

/// <summary>
/// Consomme l'event EmployeUpdated pour mettre à jour le snapshot.
/// </summary>
public class EmployeUpdatedConsumer : IConsumer<EmployeUpdatedMessage>
{
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EmployeUpdatedConsumer(
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork)
    {
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        var snapshot = await _employeRepo.GetByEmployeIdAsync(msg.EmployeId, context.CancellationToken);

        if (snapshot == null) return; // Pas encore créé, on ignore

        snapshot.MettreAJour(msg.Nom, msg.Prenom, msg.Email, msg.ManagerId, msg.ServiceId, msg.ServiceNom);
        _employeRepo.Update(snapshot);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>
/// Consomme l'initialisation annuelle des soldes depuis le service RH (début d'année).
/// </summary>
public class SoldeAnnuelInitialiseConsumer : IConsumer<SoldeAnnuelInitialiseMessage>
{
    private readonly IMediator _mediator;

    public SoldeAnnuelInitialiseConsumer(IMediator mediator) => _mediator = mediator;

    public async Task Consume(ConsumeContext<SoldeAnnuelInitialiseMessage> context)
    {
        var msg = context.Message;
        await _mediator.Send(new InitialiserSoldeCommand(
            msg.EmployeId,
            msg.AncienneteAnnees,
            msg.EstMineur,
            msg.Annee), context.CancellationToken);
    }
}