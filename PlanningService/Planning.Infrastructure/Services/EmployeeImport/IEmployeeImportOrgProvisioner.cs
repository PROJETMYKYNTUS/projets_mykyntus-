using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public interface IEmployeeImportOrgProvisioner
{
    Task<IReadOnlyList<OrgNodeCreatedReportDto>> ProvisionAsync(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct = default);
}
