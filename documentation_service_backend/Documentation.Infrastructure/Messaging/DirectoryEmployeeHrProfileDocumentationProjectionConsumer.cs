using Documentation.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace Documentation.Infrastructure.Messaging;

/// <summary>Projection profil RH canonique — RIB / CIN / CNSS sur directory_users.</summary>
public sealed class DirectoryEmployeeHrProfileDocumentationProjectionConsumer(DirectoryUserSyncService sync) :
    IConsumer<DirectoryEmployeeHrProfileChangedMessage>
{
    public Task Consume(ConsumeContext<DirectoryEmployeeHrProfileChangedMessage> context) =>
        sync.ApplyHrProfileFromMessageAsync(context.Message, context.CancellationToken);
}
