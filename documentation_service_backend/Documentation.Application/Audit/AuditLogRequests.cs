using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Audit;

public record ListAuditLogsQuery(AuditLogListQuery Query) : IRequest<PagedResponse<AuditLogResponse>>;

public sealed class ListAuditLogsQueryHandler(IAuditLogQueryAppService auditLogs)
    : IRequestHandler<ListAuditLogsQuery, PagedResponse<AuditLogResponse>>
{
    public Task<PagedResponse<AuditLogResponse>> Handle(ListAuditLogsQuery request, CancellationToken ct) =>
        auditLogs.ListAsync(request.Query, ct);
}
