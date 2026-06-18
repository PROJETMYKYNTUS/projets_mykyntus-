using PlanningService.DTOs;

namespace PlanningService.Interfaces;

public interface IFloorService
{
    /// <summary>
    /// Récupérer tous les étages
    /// </summary>
    Task<List<FloorDto>> GetAllFloorsAsync();

    /// <summary>
    /// Récupérer un étage par ID
    /// </summary>
    Task<FloorDto?> GetFloorByIdAsync(int id);

    /// <summary>
    /// Récupérer un étage avec ses services
    /// </summary>
    Task<FloorDetailDto?> GetFloorWithServicesAsync(int id);

    /// <summary>
    /// Créer un nouvel étage
    /// </summary>
    Task<FloorDto> CreateFloorAsync(CreateFloorDto dto);

    /// <summary>
    /// Mettre à jour un étage
    /// </summary>
    Task<FloorDto?> UpdateFloorAsync(int id, UpdateFloorDto dto);

    /// <summary>
    /// Supprimer un étage
    /// </summary>
    Task<bool> DeleteFloorAsync(int id);

    /// <summary>
    /// Vérifier si un étage existe
    /// </summary>
    Task<bool> FloorExistsAsync(int id);
}