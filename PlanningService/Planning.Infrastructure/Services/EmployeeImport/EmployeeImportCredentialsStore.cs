using System.Collections.Concurrent;
using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public interface IEmployeeImportCredentialsStore
{
    void Remember(Guid jobId, IReadOnlyList<EmployeeImportRowResultDto> linesWithSecrets);
    void ApplyToReport(EmployeeImportReportDto report);
    void Forget(Guid jobId);
}

/// <summary>
/// Cache mémoire one-shot des MDP d'import (jamais persistés en base).
/// Disponible tant que le process tourne et jusqu'à oubli / TTL.
/// </summary>
public sealed class EmployeeImportCredentialsStore : IEmployeeImportCredentialsStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<Guid, CacheEntry> _entries = new();

    public void Remember(Guid jobId, IReadOnlyList<EmployeeImportRowResultDto> linesWithSecrets)
    {
        var byLine = linesWithSecrets
            .Where(l => !string.IsNullOrEmpty(l.TemporaryPassword))
            .ToDictionary(
                l => l.LineNumber,
                l => new CredentialSnapshot(
                    l.TemporaryPassword!,
                    l.FirstName,
                    l.LastName,
                    l.Email));

        if (byLine.Count == 0)
            return;

        _entries[jobId] = new CacheEntry(DateTime.UtcNow, byLine);
        EvictExpired();
    }

    public void ApplyToReport(EmployeeImportReportDto report)
    {
        if (!_entries.TryGetValue(report.ImportJobId, out var entry))
            return;

        if (DateTime.UtcNow - entry.CreatedAt > Ttl)
        {
            _entries.TryRemove(report.ImportJobId, out _);
            return;
        }

        foreach (var line in report.Lignes)
        {
            if (!entry.ByLine.TryGetValue(line.LineNumber, out var snap))
                continue;

            line.TemporaryPassword = snap.Password;
            line.FirstName ??= snap.FirstName;
            line.LastName ??= snap.LastName;
            if (string.IsNullOrWhiteSpace(line.Email))
                line.Email = snap.Email;
        }
    }

    public void Forget(Guid jobId) => _entries.TryRemove(jobId, out _);

    private void EvictExpired()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var kv in _entries)
        {
            if (kv.Value.CreatedAt < cutoff)
                _entries.TryRemove(kv.Key, out _);
        }
    }

    private sealed record CredentialSnapshot(
        string Password,
        string? FirstName,
        string? LastName,
        string? Email);

    private sealed record CacheEntry(
        DateTime CreatedAt,
        Dictionary<int, CredentialSnapshot> ByLine);
}
