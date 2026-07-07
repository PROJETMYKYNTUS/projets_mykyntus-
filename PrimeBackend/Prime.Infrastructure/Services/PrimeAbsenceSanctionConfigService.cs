using Microsoft.EntityFrameworkCore;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public interface IPrimeAbsenceSanctionConfigService
{
    Task<PrimeAbsenceSanctionConfigDto> GetAsync(CancellationToken ct = default);
    Task<PrimeAbsenceSanctionConfigDto> SaveAsync(PrimeAbsenceSanctionConfigDto dto, string userId, CancellationToken ct = default);
    Task<int> GetDivisorDaysAsync(CancellationToken ct = default);
}

public sealed class PrimeAbsenceSanctionConfigService(PrimeDbContext db) : IPrimeAbsenceSanctionConfigService
{
    public async Task<PrimeAbsenceSanctionConfigDto> GetAsync(CancellationToken ct = default)
    {
        var row = await EnsureRowAsync(ct);
        return Map(row);
    }

    public async Task<PrimeAbsenceSanctionConfigDto> SaveAsync(
        PrimeAbsenceSanctionConfigDto dto,
        string userId,
        CancellationToken ct = default)
    {
        if (dto.DivisorDays <= 0)
            throw new ArgumentException("Le diviseur doit être strictement positif.");

        var row = await EnsureRowAsync(ct);
        row.DivisorDays = dto.DivisorDays;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedByUserId = userId.Trim();
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<int> GetDivisorDaysAsync(CancellationToken ct = default)
    {
        var row = await EnsureRowAsync(ct);
        return row.DivisorDays > 0 ? row.DivisorDays : PrimeAbsenceSanctionCalculator.DefaultDivisorDays;
    }

    private async Task<PrimeAbsenceSanctionConfigEntity> EnsureRowAsync(CancellationToken ct)
    {
        var row = await db.PrimeAbsenceSanctionConfigs
            .FirstOrDefaultAsync(c => c.Id == PrimeAbsenceSanctionConfigEntity.SingletonId, ct);

        if (row is not null) return row;

        row = new PrimeAbsenceSanctionConfigEntity
        {
            Id = PrimeAbsenceSanctionConfigEntity.SingletonId,
            DivisorDays = PrimeAbsenceSanctionCalculator.DefaultDivisorDays,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.PrimeAbsenceSanctionConfigs.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    private static PrimeAbsenceSanctionConfigDto Map(PrimeAbsenceSanctionConfigEntity row) => new()
    {
        DivisorDays = row.DivisorDays,
        UpdatedAt = row.UpdatedAt,
        UpdatedByUserId = row.UpdatedByUserId,
    };
}
