using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface INotificationAppService
{
    Task<IReadOnlyList<ReferralNotificationDto>> ListAsync(
        string? role,
        string? userId,
        string? projectId,
        CancellationToken ct = default);

    Task<NotificationPreferencesDto> GetPreferencesAsync(CancellationToken ct = default);
    Task<NotificationPreferencesDto> UpdatePreferencesAsync(NotificationPreferencesDto body, CancellationToken ct = default);
    Task<bool> MarkReadAsync(string id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}
