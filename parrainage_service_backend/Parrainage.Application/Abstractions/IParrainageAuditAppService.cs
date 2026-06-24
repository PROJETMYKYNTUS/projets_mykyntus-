using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IParrainageAuditAppService
{
    Task<IReadOnlyList<AuditLogDto>> ListAsync(int? take, CancellationToken ct = default);
    Task<AuditLogDto> CreateAsync(CreateAuditRequest body, CancellationToken ct = default);
}
