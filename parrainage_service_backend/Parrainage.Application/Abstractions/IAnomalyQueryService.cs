using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IAnomalyQueryService
{
    Task<AnomaliesDto> GetAsync(CancellationToken ct = default);
}
