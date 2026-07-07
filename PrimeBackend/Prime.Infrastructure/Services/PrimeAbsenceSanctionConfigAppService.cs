using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Services;

public sealed class PrimeAbsenceSanctionConfigAppService(
    IPrimeAbsenceSanctionConfigService config,
    IPrimeRequestUserResolver userResolver) : IPrimeAbsenceSanctionConfigAppService
{
    public Task<PrimeAbsenceSanctionConfigDto> GetAsync(CancellationToken ct = default) =>
        config.GetAsync(ct);

    public async Task<PrimeAbsenceSanctionConfigDto> SaveAsync(
        SavePrimeAbsenceSanctionConfigRequest body,
        CancellationToken ct = default)
    {
        var resolved = await userResolver.TryResolveForValidationAsync(body.UserId, body.Role, ct)
            ?? throw new PrimeApiException(401, "Utilisateur invalide.");

        var role = resolved.Role?.Trim() ?? resolved.Employee.Role?.Trim() ?? "";
        if (!string.Equals(role, "Admin", StringComparison.Ordinal) &&
            !string.Equals(role, "RH", StringComparison.Ordinal))
            throw new PrimeApiException(403, "Seuls Admin et RH peuvent modifier la configuration des sanctions.");

        return await config.SaveAsync(
            new PrimeAbsenceSanctionConfigDto { DivisorDays = body.DivisorDays },
            resolved.UserId,
            ct);
    }
}
