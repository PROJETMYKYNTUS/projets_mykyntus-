namespace PlanningService.DTOs;

/// <summary>
/// DTO pour créer un nouvel étage
/// </summary>
public class CreateFloorDto
{
    public string Name { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO pour mettre à jour un étage
/// </summary>
public class UpdateFloorDto
{
    public string Name { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO de réponse pour un étage
/// </summary>
public class FloorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string? Description { get; set; }
    public int ServicesCount { get; set; } // Nombre de services dans cet étage
}

/// <summary>
/// DTO de réponse détaillée avec les services
/// </summary>
public class FloorDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string? Description { get; set; }
    public List<ServiceDto> Services { get; set; } = new();
}