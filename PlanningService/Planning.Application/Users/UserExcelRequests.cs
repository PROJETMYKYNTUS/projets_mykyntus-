using MediatR;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.Application.Users;

public record DownloadUserImportTemplateQuery : IRequest<byte[]>;

public sealed class DownloadUserImportTemplateQueryHandler(IUserLegacyExcelService excel)
    : IRequestHandler<DownloadUserImportTemplateQuery, byte[]>
{
    public Task<byte[]> Handle(DownloadUserImportTemplateQuery request, CancellationToken ct) =>
        Task.FromResult(excel.BuildImportTemplate());
}

public record ImportUsersFromExcelCommand(Stream ExcelStream) : IRequest<ImportResultDto>;

public sealed class ImportUsersFromExcelCommandHandler(IUserLegacyExcelService excel)
    : IRequestHandler<ImportUsersFromExcelCommand, ImportResultDto>
{
    public Task<ImportResultDto> Handle(ImportUsersFromExcelCommand request, CancellationToken ct) =>
        excel.ImportUsersAsync(request.ExcelStream, ct);
}

public record ExportUsersToExcelQuery : IRequest<byte[]>;

public sealed class ExportUsersToExcelQueryHandler(IUserLegacyExcelService excel)
    : IRequestHandler<ExportUsersToExcelQuery, byte[]>
{
    public Task<byte[]> Handle(ExportUsersToExcelQuery request, CancellationToken ct) =>
        excel.ExportUsersAsync(ct);
}
