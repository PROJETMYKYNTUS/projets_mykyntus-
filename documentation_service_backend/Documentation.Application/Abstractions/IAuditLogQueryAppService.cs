using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IAuditLogQueryAppService
{
    Task<PagedResponse<AuditLogResponse>> ListAsync(AuditLogListQuery query, CancellationToken ct = default);
}
