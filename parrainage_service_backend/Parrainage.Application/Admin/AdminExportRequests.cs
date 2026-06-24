using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Admin;

public record ExportAdminSnapshotQuery : IRequest<ExportSnapshotDto>;
public sealed class ExportAdminSnapshotQueryHandler(IAdminExportAppService adminExport)
    : IRequestHandler<ExportAdminSnapshotQuery, ExportSnapshotDto>
{
    public Task<ExportSnapshotDto> Handle(ExportAdminSnapshotQuery request, CancellationToken ct) =>
        adminExport.ExportAsync(ct);
}
