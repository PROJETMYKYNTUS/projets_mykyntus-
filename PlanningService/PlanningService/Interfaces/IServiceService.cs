using PlanningService.DTOs;

namespace PlanningService.Interfaces;

public interface IServiceService
{
    /// <summary>
    /// Récupérer tous les services
    /// </summary>
    Task<List<ServiceDto>> GetAllServicesAsync();

    /// <summary>
    /// Récupérer les services d'un étage spécifique
    /// </summary>
    Task<List<ServiceDto>> GetServicesByFloorIdAsync(int floorId);

    /// <summary>
    /// Récupérer un service par ID
    /// </summary>
    Task<ServiceDto?> GetServiceByIdAsync(int id);

    /// <summary>
    /// Récupérer un service avec ses sous-services
    /// </summary>
    Task<ServiceDetailDto?> GetServiceWithSubServicesAsync(int id);

    /// <summary>
    /// Créer un nouveau service
    /// </summary>
    Task<ServiceDto> CreateServiceAsync(CreateServiceDto dto);

    /// <summary>
    /// Mettre à jour un service
    /// </summary>
    Task<ServiceDto?> UpdateServiceAsync(int id, UpdateServiceDto dto);

    /// <summary>
    /// Supprimer un service
    /// </summary>
    Task<bool> DeleteServiceAsync(int id);

    /// <summary>
    /// Vérifier si un service existe
    /// </summary>
    Task<bool> ServiceExistsAsync(int id);

    /// <summary>
    /// Vérifier si le code du service est unique
    /// </summary>
    Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null);
}