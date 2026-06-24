namespace Prime.Application.DTOs;

public record OperationalDepartmentOverviewDto(
    string Id,
    string Code,
    string Name,
    string? ManagerEmployeeId,
    IReadOnlyList<OrgPoleOverviewDto> Poles);

public record OrgPoleOverviewDto(
    string Id,
    string Name,
    IReadOnlyList<OrgCelluleOverviewDto> Cellules);

public record OrgCelluleOverviewDto(
    string Id,
    string Name,
    IReadOnlyList<OrgServiceOverviewDto> Services);

public record OrgServiceOverviewDto(string Id, string Name);

public record OperationalOrgTreeDto(
    IReadOnlyList<OperationalDepartmentOverviewDto> OperationalDepartments,
    IReadOnlyList<OrgPoleOverviewDto> UnassignedPoles);
