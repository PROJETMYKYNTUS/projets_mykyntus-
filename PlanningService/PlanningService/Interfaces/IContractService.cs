// Interfaces/IContractService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using PlanningService.DTOs;

namespace PlanningService.Interfaces
{
    public interface IContractService
    {
        // ── CRUD ──
        Task<IEnumerable<ContractResponseDto>> GetAllContractsAsync();
        Task<ContractResponseDto?> GetContractByIdAsync(int id);
        Task<IEnumerable<ContractResponseDto>> GetContractsByUserIdAsync(int userId);
        Task<ContractResponseDto> CreateContractAsync(CreateContractDto dto);
        Task<ContractResponseDto?> UpdateContractAsync(int id, UpdateContractDto dto);
        Task<bool> DeleteContractAsync(int id);

        // ── Notifications LIVE ──
        Task<IEnumerable<NotificationResponseDto>> GetUnreadNotificationsAsync();
        Task<int> GetUnreadNotificationsCountAsync();

        // Gardées pour compatibilité (ne font rien dans la nouvelle logique)
        Task MarkNotificationAsReadAsync(int notificationId);
        Task MarkAllNotificationsAsReadAsync();
        Task CheckAndGenerateAlertsAsync();
    }
}