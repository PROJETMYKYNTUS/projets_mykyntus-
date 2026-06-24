using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IAdminExportAppService
{
    Task<ExportSnapshotDto> ExportAsync(CancellationToken ct = default);
}
