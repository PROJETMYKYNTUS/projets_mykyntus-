using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentationBackend.Infrastructure.Ai;

public sealed class AiApiKeyResolver(DocumentationDbContext db) : IAiApiKeyResolver
{
    /// <inheritdoc />
    public async Task<string?> GetActiveApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return await db.AiApiKeys.AsNoTracking()
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.ApiKey)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetFallbackApiKeysAsync(CancellationToken cancellationToken = default)
    {
        return await db.AiApiKeys.AsNoTracking()
            .Where(k => !string.IsNullOrWhiteSpace(k.ApiKey))
            .OrderByDescending(k => k.IsActive)
            .ThenByDescending(k => k.CreatedAt)
            .Select(k => k.ApiKey.Trim())
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
