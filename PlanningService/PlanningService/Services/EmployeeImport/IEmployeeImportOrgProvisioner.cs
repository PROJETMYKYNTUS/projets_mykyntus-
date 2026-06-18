using PlanningService.DTOs;

namespace PlanningService.Services.EmployeeImport;

public interface IEmployeeImportOrgProvisioner
{
    Task<IReadOnlyList<OrgNodeCreatedReportDto>> ProvisionAsync(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct = default);
}
