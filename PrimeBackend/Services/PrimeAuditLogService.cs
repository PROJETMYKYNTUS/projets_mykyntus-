using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

public sealed class PrimeAuditLogService(PrimeDbContext? db, IHttpContextAccessor httpContextAccessor)
{
    public async Task RecordNavigationAsync(
        string userId,
        string userDisplayName,
        string role,
        string route,
        CancellationToken ct = default)
    {
        await RecordAsync(userId, userDisplayName, role, "PageView", "Route",
            route.Trim().Length > 512 ? route.Trim()[..512] : route.Trim(),
            JsonSerializer.Serialize(new { route }), ct);
    }

    public async Task RecordAsync(
        string userId,
        string userDisplayName,
        string role,
        string action,
        string entityType,
        string? entityId,
        string? detailJson,
        CancellationToken ct = default)
    {
        if (db is null || string.IsNullOrWhiteSpace(userId)) return;

        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var row = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            At = DateTimeOffset.UtcNow,
            UserId = userId.Trim(),
            UserDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? userId.Trim() : userDisplayName.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? "" : role.Trim(),
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim(),
            DetailJson = detailJson,
            IpAddress = ip,
        };
        db.AuditLogs.Add(row);
        await db.SaveChangesAsync(ct);
    }
}
