using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Audit;

public record ListParrainageAuditLogsQuery(int? Take) : IRequest<IReadOnlyList<AuditLogDto>>;
public sealed class ListParrainageAuditLogsQueryHandler(IParrainageAuditAppService audit)
    : IRequestHandler<ListParrainageAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public Task<IReadOnlyList<AuditLogDto>> Handle(ListParrainageAuditLogsQuery request, CancellationToken ct) =>
        audit.ListAsync(request.Take, ct);
}

public record CreateParrainageAuditLogCommand(CreateAuditRequest Body) : IRequest<AuditLogDto>;
public sealed class CreateParrainageAuditLogCommandHandler(IParrainageAuditAppService audit)
    : IRequestHandler<CreateParrainageAuditLogCommand, AuditLogDto>
{
    public Task<AuditLogDto> Handle(CreateParrainageAuditLogCommand request, CancellationToken ct) =>
        audit.CreateAsync(request.Body, ct);
}
