namespace PlanningService.DTOs;

/// <summary>
/// DTO pour créer un nouveau sous-service
/// </summary>
public class CreateSubServiceDto
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// DTO pour mettre à jour un sous-service
/// </summary>
public class UpdateSubServiceDto
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// DTO de réponse pour un sous-service
/// </summary>
public class SubServiceDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int EmployeesCount { get; set; } // Nombre d'employés
}

/// <summary>
/// DTO de réponse détaillée avec les employés
/// </summary>
public class SubServiceDetailDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<UserSimpleDto> Employees { get; set; } = new(); // Liste des employés
}

/// <summary>
/// DTO simple pour un utilisateur (utilisé dans les listes)
/// </summary>
public class UserSimpleDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}