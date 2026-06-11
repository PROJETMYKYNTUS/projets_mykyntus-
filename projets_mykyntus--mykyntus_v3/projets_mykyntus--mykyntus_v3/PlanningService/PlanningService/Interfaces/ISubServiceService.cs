using PlanningService.DTOs;

namespace PlanningService.Interfaces;

public interface ISubServiceService
{
    /// <summary>
    /// Récupérer tous les sous-services
    /// </summary>
    Task<List<SubServiceDto>> GetAllSubServicesAsync();

    /// <summary>
    /// Récupérer les sous-services d'un service spécifique
    /// </summary>
    Task<List<SubServiceDto>> GetSubServicesByServiceIdAsync(int serviceId);

    /// <summary>
    /// Récupérer un sous-service par ID
    /// </summary>
    Task<SubServiceDto?> GetSubServiceByIdAsync(int id);

    /// <summary>
    /// Récupérer un sous-service avec ses employés
    /// </summary>
    Task<SubServiceDetailDto?> GetSubServiceWithEmployeesAsync(int id);

    /// <summary>
    /// Créer un nouveau sous-service
    /// </summary>
    Task<SubServiceDto> CreateSubServiceAsync(CreateSubServiceDto dto);

    /// <summary>
    /// Mettre à jour un sous-service
    /// </summary>
    Task<SubServiceDto?> UpdateSubServiceAsync(int id, UpdateSubServiceDto dto);

    /// <summary>
    /// Supprimer un sous-service
    /// </summary>
    Task<bool> DeleteSubServiceAsync(int id);

    /// <summary>
    /// Vérifier si un sous-service existe
    /// </summary>
    Task<bool> SubServiceExistsAsync(int id);

    /// <summary>
    /// Vérifier si le code du sous-service est unique
    /// </summary>
    Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null);
}