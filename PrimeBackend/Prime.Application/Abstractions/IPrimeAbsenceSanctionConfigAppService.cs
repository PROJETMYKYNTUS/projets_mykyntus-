using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimeAbsenceSanctionConfigAppService
{
    Task<PrimeAbsenceSanctionConfigDto> GetAsync(CancellationToken ct = default);

    Task<PrimeAbsenceSanctionConfigDto> SaveAsync(
        SavePrimeAbsenceSanctionConfigRequest body,
        CancellationToken ct = default);
}
