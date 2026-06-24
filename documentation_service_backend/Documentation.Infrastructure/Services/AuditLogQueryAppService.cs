using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Mapping;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Documentation.Infrastructure.Services;

public sealed class AuditLogQueryAppService(DocumentationDbContext db) : IAuditLogQueryAppService
{
    public async Task<PagedResponse<AuditLogResponse>> ListAsync(AuditLogListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        if (!TryParseSortOrder(query.SortOrder, out var desc))
            throw new DocumentationApiException(400, "sortOrder doit être « asc » ou « desc ».");

        AppRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!AppRoleHeaderParser.TryParse(query.Role, out var rf))
                throw new DocumentationApiException(400, "role de filtre invalide (pilote, coach, manager, rp, rh, admin, audit).");
            roleFilter = rf;
        }

        if (!TryParseAuditSortField(query.SortBy, out var sortField))
            throw new DocumentationApiException(400, "sortBy doit être occurredAt ou action.");

        var dbQuery = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var a = query.Action.Trim();
            dbQuery = dbQuery.Where(x => x.Action.Contains(a));
        }

        if (roleFilter.HasValue)
        {
            var actorIdsForRole = await db.DirectoryUsers.AsNoTracking()
                .Where(u => u.Role == roleFilter.Value)
                .Select(u => u.Id)
                .ToListAsync(ct);
            dbQuery = actorIdsForRole.Count > 0
                ? dbQuery.Where(x => x.ActorUserId.HasValue && actorIdsForRole.Contains(x.ActorUserId.Value))
                : dbQuery.Where(static x => false);
        }

        dbQuery = ApplyAuditSort(dbQuery, sortField, desc);

        var total = await dbQuery.CountAsync(ct);
        var rows = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var auditActorIds = rows.Where(a => a.ActorUserId.HasValue).Select(a => a.ActorUserId!.Value).ToArray();
        var auditNames = await DocumentRequestMappingHelper.LoadDisplayNamesAsync(db, auditActorIds, ct);

        var items = rows.Select(a => new AuditLogResponse(
            a.Id.ToString(),
            a.OccurredAt.ToString("O"),
            a.ActorUserId.HasValue ? DocumentRequestMappingHelper.ResolveName(auditNames, a.ActorUserId.Value) : null,
            a.ActorUserId.HasValue ? a.ActorUserId.Value.ToString() : null,
            a.Action,
            a.EntityType,
            a.EntityId.HasValue ? a.EntityId.Value.ToString() : null,
            a.Success,
            a.ErrorMessage,
            a.CorrelationId.HasValue ? a.CorrelationId.Value.ToString() : null)).ToList();

        return new PagedResponse<AuditLogResponse>(items, total, page, pageSize);
    }

    private enum AuditSortField
    {
        OccurredAt,
        Action,
    }

    private static bool TryParseSortOrder(string? sortOrder, out bool descending)
    {
        descending = true;
        if (string.IsNullOrWhiteSpace(sortOrder))
            return true;

        var s = sortOrder.Trim().ToLowerInvariant();
        if (s == "asc")
        {
            descending = false;
            return true;
        }

        return s == "desc";
    }

    private static bool TryParseAuditSortField(string? sortBy, out AuditSortField field)
    {
        field = AuditSortField.OccurredAt;
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        switch (sortBy.Trim().ToLowerInvariant())
        {
            case "occurredat":
                field = AuditSortField.OccurredAt;
                return true;
            case "action":
                field = AuditSortField.Action;
                return true;
            default:
                return false;
        }
    }

    private static IQueryable<AuditLog> ApplyAuditSort(IQueryable<AuditLog> q, AuditSortField sortField, bool desc) =>
        sortField switch
        {
            AuditSortField.Action => desc
                ? q.OrderByDescending(a => a.Action).ThenByDescending(a => a.OccurredAt)
                : q.OrderBy(a => a.Action).ThenByDescending(a => a.OccurredAt),
            _ => desc ? q.OrderByDescending(a => a.OccurredAt) : q.OrderBy(a => a.OccurredAt),
        };
}
