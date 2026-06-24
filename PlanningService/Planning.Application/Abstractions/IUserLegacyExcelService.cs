using Planning.Application.DTOs;

namespace Planning.Application.Abstractions;

public interface IUserLegacyExcelService
{
    byte[] BuildImportTemplate();

    Task<ImportResultDto> ImportUsersAsync(Stream excelStream, CancellationToken ct = default);

    Task<byte[]> ExportUsersAsync(CancellationToken ct = default);
}
