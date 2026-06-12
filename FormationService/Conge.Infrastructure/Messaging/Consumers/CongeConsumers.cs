using Conge.Application.Commands.InitialiserSolde;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;
using MediatR;

namespace Conge.Infrastructure.Messaging.Consumers;

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
        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? KyntusRoleNames.Employee : msg.Role;

        var exists = await _employeRepo.ExistsAsync(msg.EmployeId, context.CancellationToken);
        if (!exists)
        {
            var snapshot = EmployeSnapshot.Creer(
                msg.EmployeId,
                msg.Nom,
                msg.Prenom,
                msg.Email,
                managerId,
                msg.ServiceId,
                msg.ServiceNom,
                msg.DateEmbauche,
                msg.EstMineur,
                role);
            await _employeRepo.AddAsync(snapshot, context.CancellationToken);
            await _unitOfWork.SaveChangesAsync(context.CancellationToken);
        }

        var employe = await _employeRepo.GetByEmployeIdAsync(msg.EmployeId, context.CancellationToken);
        var anciennete = employe!.GetAncienneteAnnees();
        await _mediator.Send(new InitialiserSoldeCommand(
            msg.EmployeId,
            anciennete,
            msg.EstMineur,
            DateTime.Today.Year), context.CancellationToken);
    }
}

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

        if (snapshot == null) return;

        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? snapshot.Role : msg.Role;

        snapshot.MettreAJour(msg.Nom, msg.Prenom, msg.Email, managerId, msg.ServiceId, msg.ServiceNom, role);
        _employeRepo.Update(snapshot);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

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
