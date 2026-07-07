// Interfaces/IContractService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Planning.Application.DTOs;

namespace Planning.Application.Abstractions
{
    public interface IContractService
    {
        // -- CRUD --
        Task<IEnumerable<ContractResponseDto>> GetAllContractsAsync();
        Task<ContractResponseDto?> GetContractByIdAsync(int id);
        Task<IEnumerable<ContractResponseDto>> GetContractsByUserIdAsync(int userId);
        Task<EmploymentSummaryDto?> GetEmploymentSummaryByEmployeeGuidAsync(Guid employeeGuid);
        Task<ContractResponseDto> CreateContractAsync(CreateContractDto dto);
        Task<ContractResponseDto?> UpdateContractAsync(int id, UpdateContractDto dto);
        Task<bool> DeleteContractAsync(int id);
        Task<ContractResponseDto?> ConfirmProbationPeriodAsync(int contractId);

        // -- Notifications LIVE --
        Task<IEnumerable<NotificationResponseDto>> GetUnreadNotificationsAsync();
        Task<int> GetUnreadNotificationsCountAsync();

        // Gard�es pour compatibilit� (ne font rien dans la nouvelle logique)
        Task MarkNotificationAsReadAsync(int notificationId);
        Task MarkAllNotificationsAsReadAsync();
        Task CheckAndGenerateAlertsAsync();
    }
}