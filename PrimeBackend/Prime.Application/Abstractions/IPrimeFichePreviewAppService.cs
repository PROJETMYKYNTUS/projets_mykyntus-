using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimeFichePreviewAppService
{
    Task<MergedFichePreviewContextDto> GetMergedPreviewContextAsync(
        Guid ficheId,
        string? userId,
        string? role,
        CancellationToken ct = default);
}
