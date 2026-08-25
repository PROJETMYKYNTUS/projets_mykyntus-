using System.Globalization;
using Prime.Application.DTOs;

namespace Prime.Application;

public static class CommonLinePonderationPeriod
{
    public static DateTimeOffset StartOfUtcDay(DateTimeOffset at) =>
        new(at.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>Version active à l’instant présent (fiches non figées, écran de config).</summary>
    public static DateTimeOffset ForLiveResolve(DateTimeOffset? now = null) =>
        StartOfUtcDay(now ?? DateTimeOffset.UtcNow);

    /// <summary>1er jour du mois suivant (défaut pour une nouvelle version de pondération).</summary>
    public static DateTimeOffset DefaultEffectiveFromForNewVersion(DateTimeOffset? now = null)
    {
        var at = StartOfUtcDay(now ?? DateTimeOffset.UtcNow);
        return new DateTimeOffset(at.Year, at.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
    }

    public static DateTimeOffset FromPeriod(string? period, DateTimeOffset? fallback = null)
    {
        var p = (period ?? "").Trim();
        if (p.Length >= 7 &&
            DateTime.TryParseExact(
                p[..7] + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var d))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc));
        }

        return fallback ?? DateTimeOffset.UtcNow;
    }

    public static decimal? NormalizePct(decimal? value)
    {
        if (value is null) return null;
        if (value < 0 || value > 100)
            throw new ArgumentException("Pondération Prime/Challenge comprise entre 0 et 100.");
        return Math.Round(value.Value, 4, MidpointRounding.AwayFromZero);
    }

    public static List<ServicePoleLinePonderationDto> ToPoleLineDtos(
        string serviceId,
        IEnumerable<EffectiveCommonLinePonderationDto> items) =>
        items.Select(x => new ServicePoleLinePonderationDto
        {
            Id = x.VersionId ?? Guid.Empty,
            ServiceId = serviceId,
            TemplateStableId = x.TemplateStableId,
            Label = x.Label,
            SortOrder = x.SortOrder,
            PonderationPrimePct = x.PonderationPrimePct,
            PonderationChallengePct = x.PonderationChallengePct,
            CreatedAt = x.EffectiveFrom ?? default,
            UpdatedAt = null,
            SourceScope = x.SourceScope,
            Inherited = x.Inherited,
            EffectiveFrom = x.EffectiveFrom,
        }).ToList();
}
