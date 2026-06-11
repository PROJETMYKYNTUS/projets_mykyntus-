namespace PlanningService.DTOs;

/// <summary>
/// DTO pour créer un nouveau service
/// </summary>
public class CreateServiceDto
{
    public int FloorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// DTO pour mettre à jour un service
/// </summary>
public class UpdateServiceDto
{
    public int FloorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// DTO de réponse pour un service
/// </summary>
public class ServiceDto
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int SubServicesCount { get; set; } // Nombre de sous-services
}

/// <summary>
/// DTO de réponse détaillée avec les sous-services
/// </summary>
public class ServiceDetailDto
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<SubServiceDto> SubServices { get; set; } = new();
}