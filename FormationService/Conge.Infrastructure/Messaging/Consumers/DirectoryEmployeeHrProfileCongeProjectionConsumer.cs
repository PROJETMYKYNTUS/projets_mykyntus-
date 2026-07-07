using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Projection profil RH canonique — met à jour EstMineur depuis DateNaissance.</summary>
public sealed class DirectoryEmployeeHrProfileCongeProjectionConsumer(
    IEmployeSnapshotRepository employeRepo,
    IUnitOfWork unitOfWork) : IConsumer<DirectoryEmployeeHrProfileChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeHrProfileChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted)
            return;

        var snapshot = await employeRepo.GetByEmployeIdAsync(msg.EmployeeId, context.CancellationToken);
        if (snapshot is null)
            return;

        snapshot.MettreAJourEstMineur(Conge.Domain.Entities.EmployeSnapshot.ComputeEstMineur(msg.DateNaissance));
        employeRepo.Update(snapshot);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
