using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Notifications;

public record ListNotificationsQuery(string? Role, string? UserId, string? ProjectId)
    : IRequest<IReadOnlyList<ReferralNotificationDto>>;
public sealed class ListNotificationsQueryHandler(INotificationAppService notifications)
    : IRequestHandler<ListNotificationsQuery, IReadOnlyList<ReferralNotificationDto>>
{
    public Task<IReadOnlyList<ReferralNotificationDto>> Handle(ListNotificationsQuery request, CancellationToken ct) =>
        notifications.ListAsync(request.Role, request.UserId, request.ProjectId, ct);
}

public record GetNotificationPreferencesQuery : IRequest<NotificationPreferencesDto>;
public sealed class GetNotificationPreferencesQueryHandler(INotificationAppService notifications)
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    public Task<NotificationPreferencesDto> Handle(GetNotificationPreferencesQuery request, CancellationToken ct) =>
        notifications.GetPreferencesAsync(ct);
}

public record UpdateNotificationPreferencesCommand(NotificationPreferencesDto Body)
    : IRequest<NotificationPreferencesDto>;
public sealed class UpdateNotificationPreferencesCommandHandler(INotificationAppService notifications)
    : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesDto>
{
    public Task<NotificationPreferencesDto> Handle(UpdateNotificationPreferencesCommand request, CancellationToken ct) =>
        notifications.UpdatePreferencesAsync(request.Body, ct);
}

public record MarkNotificationReadCommand(string Id) : IRequest<bool>;
public sealed class MarkNotificationReadCommandHandler(INotificationAppService notifications)
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    public Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken ct) =>
        notifications.MarkReadAsync(request.Id, ct);
}

public record MarkAllNotificationsReadCommand : IRequest<Unit>;
public sealed class MarkAllNotificationsReadCommandHandler(INotificationAppService notifications)
    : IRequestHandler<MarkAllNotificationsReadCommand, Unit>
{
    public async Task<Unit> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return Unit.Value;
    }
}
