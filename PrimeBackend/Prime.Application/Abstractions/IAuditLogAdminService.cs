using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public sealed record AuditLogListFilter(
    string? UserId,
    string? Role,
    string? Action,
    string? EntityType,
    string? EntityId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Take);

public interface IAuditLogAdminService
{
    Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogListFilter filter, CancellationToken ct = default);
    Task RecordNavigationAsync(RecordAuditNavigationRequest body, CancellationToken ct = default);
}
