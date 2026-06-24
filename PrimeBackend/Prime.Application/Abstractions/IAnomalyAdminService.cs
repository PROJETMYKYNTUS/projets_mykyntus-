using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public sealed record AnomalyListFilter(
    string? Status,
    string? Type,
    string? Severity,
    string? Period,
    string? ServiceId,
    string? CelluleId,
    string? PoleId);

public interface IAnomalyAdminService
{
    Task<IReadOnlyList<AnomalyDto>> ListAsync(AnomalyListFilter filter, CancellationToken ct = default);
    Task<AnomalyDto?> UpdateStatusAsync(Guid id, UpdateAnomalyStatusBody body, CancellationToken ct = default);
    Task<int> RecomputeAllAsync(CancellationToken ct = default);
}
