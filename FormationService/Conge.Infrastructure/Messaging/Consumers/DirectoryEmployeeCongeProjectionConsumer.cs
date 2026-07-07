using Conge.Application.Commands.InitialiserSolde;
using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;
using MediatR;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Projection depuis Employee Directory (source canonique employé).</summary>
public sealed class DirectoryEmployeeCongeProjectionConsumer(
    IEmployeSnapshotRepository employeRepo,
    IUnitOfWork unitOfWork,
    IMediator mediator) : IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        var snapshot = await employeRepo.GetByEmployeIdAsync(msg.EmployeeId, context.CancellationToken);

        if (msg.IsDeleted || !msg.IsActive)
        {
            if (snapshot is not null)
            {
                employeRepo.Remove(snapshot);
                await unitOfWork.SaveChangesAsync(context.CancellationToken);
            }
            return;
        }

        var managerId = msg.SuperviseurId ?? msg.ParentId ?? Guid.Empty;
        var serviceId = Guid.TryParse(msg.ServiceId, out var parsedSvc) ? parsedSvc : Guid.Empty;
        var serviceNom = msg.ServiceId ?? string.Empty;
        var isNew = snapshot is null;

        if (isNew)
        {
            snapshot = Conge.Domain.Entities.EmployeSnapshot.Creer(
                msg.EmployeeId,
                msg.LastName,
                msg.FirstName,
                msg.Email,
                managerId,
                serviceId,
                serviceNom,
                msg.HireDate ?? DateTime.UtcNow,
                false,
                msg.Role);
            await employeRepo.AddAsync(snapshot, context.CancellationToken);
        }
        else
        {
            snapshot!.MettreAJour(msg.LastName, msg.FirstName, msg.Email, managerId, serviceId, serviceNom, msg.Role, msg.HireDate);
            employeRepo.Update(snapshot);
        }

        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        if (isNew)
        {
            var employe = await employeRepo.GetByEmployeIdAsync(msg.EmployeeId, context.CancellationToken);
            var anciennete = employe!.GetAncienneteAnnees();
            await mediator.Send(new InitialiserSoldeCommand(
                msg.EmployeeId,
                anciennete,
                false,
                DateTime.Today.Year), context.CancellationToken);
        }
    }
}
