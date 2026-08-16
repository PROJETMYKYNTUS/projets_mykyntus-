using Conge.Domain.Entities;
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
        // Legacy Guid ServiceId : parse si possible ; sinon conserver Empty (OrgServiceId porte l'ID Directory).
        var serviceId = Guid.TryParse(msg.ServiceId, out var parsedSvc) ? parsedSvc : Guid.Empty;
        var orgServiceId = msg.ServiceId;
        var serviceNom = msg.ServiceId ?? string.Empty;
        var isNew = snapshot is null;

        if (isNew)
        {
            snapshot = EmployeSnapshot.Creer(
                msg.EmployeeId,
                msg.LastName,
                msg.FirstName,
                msg.Email,
                managerId,
                serviceId,
                serviceNom,
                msg.HireDate ?? DateTime.UtcNow,
                false,
                msg.Role,
                msg.PoleId,
                msg.CelluleId,
                orgServiceId,
                msg.BusinessDepartmentId);
            await employeRepo.AddAsync(snapshot, context.CancellationToken);
        }
        else
        {
            snapshot!.MettreAJour(
                msg.LastName,
                msg.FirstName,
                msg.Email,
                managerId,
                serviceId,
                serviceNom,
                msg.Role,
                msg.HireDate,
                msg.PoleId,
                msg.CelluleId,
                orgServiceId,
                msg.BusinessDepartmentId);
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
