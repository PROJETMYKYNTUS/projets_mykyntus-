using Planning.Application.DTOs.Planning;

namespace Planning.Application.Abstractions;

public interface IPlanningPendingRequestsAlertService
{
    Task<PendingRequestsSummaryDto> GetSummaryAsync(int? viewerUserId = null, int maxItems = 50);

    /// <summary>
    /// Rappels J-1 génération : RH/Admin (global) + superviseurs (périmètre).
    /// Idempotent via LastPendingJ1ReminderDate.
    /// </summary>
    Task<bool> SendJ1RemindersAsync(DateOnly localDate);

    /// <summary>
    /// Alerte RH après génération / phase validation. Une fois par weekCode.
    /// </summary>
    Task<bool> SendValidationRemindersAsync(string weekCode);
}
