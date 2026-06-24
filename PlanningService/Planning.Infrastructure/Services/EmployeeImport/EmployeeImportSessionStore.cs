using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services.EmployeeImport;

public interface IEmployeeImportSessionStore
{
    Task<Guid> SaveAsync(string fileName, ParsedImportFile parsed, CancellationToken ct = default);
    Task<ParsedImportFile?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task<string?> GetFileNameAsync(Guid sessionId, CancellationToken ct = default);
}

public class EmployeeImportSessionStore(AppDbContext db) : IEmployeeImportSessionStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public async Task<Guid> SaveAsync(string fileName, ParsedImportFile parsed, CancellationToken ct = default)
    {
        await PurgeExpiredAsync(ct);

        var session = new EmployeeImportSession
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            HeadersJson = JsonSerializer.Serialize(parsed.Headers, JsonOpts),
            RowsJson = JsonSerializer.Serialize(parsed.Rows, JsonOpts),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(SessionTtl)
        };

        db.EmployeeImportSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task<ParsedImportFile?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.EmployeeImportSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.ExpiresAt > DateTime.UtcNow, ct);

        if (session is null)
            return null;

        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson, JsonOpts) ?? [];
        var rows = JsonSerializer.Deserialize<List<List<string>>>(session.RowsJson, JsonOpts) ?? [];
        return new ParsedImportFile(headers, rows.Select(r => (IReadOnlyList<string>)r).ToList());
    }

    public async Task<string?> GetFileNameAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await db.EmployeeImportSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.ExpiresAt > DateTime.UtcNow)
            .Select(s => s.FileName)
            .FirstOrDefaultAsync(ct);
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        var expired = await db.EmployeeImportSessions
            .Where(s => s.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(ct);
        if (expired.Count == 0)
            return;
        db.EmployeeImportSessions.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
    }
}
