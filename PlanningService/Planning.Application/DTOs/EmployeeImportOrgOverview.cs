namespace Planning.Application.DTOs;

/// <summary>Vue Organisation RH (Directory) pour validation des responsables à l'import.</summary>
public sealed class EmployeeImportOrgOverview
{
    public List<ImportOrgEtageDto> Etages { get; init; } = [];
    public List<ImportOrgServiceNodeDto> Services { get; init; } = [];
    public List<ImportOrgSousServiceDto> SousServices { get; init; } = [];
    public List<ImportOrgEmployeeDto> Employees { get; init; } = [];
    public List<ImportOrgManagerEtageDto> ManagerEtage { get; init; } = [];
    public List<ImportOrgSupervisorServiceDto> SupervisorService { get; init; } = [];
    public List<ImportOrgCoachSousServiceDto> CoachSousService { get; init; } = [];
}

public sealed class ImportOrgEtageDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class ImportOrgServiceNodeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string EtageId { get; set; } = "";
}

public sealed class ImportOrgSousServiceDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public sealed class ImportOrgEmployeeDto
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ParentId { get; set; }
    public string? ServiceId { get; set; }
}

public sealed class ImportOrgManagerEtageDto
{
    public string UserId { get; set; } = "";
    public string EtageId { get; set; } = "";
}

public sealed class ImportOrgSupervisorServiceDto
{
    public string UserId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public sealed class ImportOrgCoachSousServiceDto
{
    public string UserId { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string SousServiceId { get; set; } = "";
}
