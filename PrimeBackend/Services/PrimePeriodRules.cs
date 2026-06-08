using System.Globalization;
using System.Text.RegularExpressions;

namespace PrimeBackend.Services;

/// <summary>Règles métier sur les périodes PRIME (YYYY-MM) — mois civil terminé uniquement.</summary>
public static partial class PrimePeriodRules
{
    private static readonly Regex PeriodRegex = PeriodFormat();

    public static bool TryParsePeriod(string? period, out int year, out int month)
    {
        year = 0;
        month = 0;
        var t = (period ?? string.Empty).Trim();
        var m = PeriodRegex.Match(t);
        if (!m.Success) return false;
        year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        month = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12;
    }

    /// <summary>Vrai si le mois civil est entièrement terminé (on est au 1er du mois suivant ou après).</summary>
    public static bool IsClosedPeriod(string period, DateTimeOffset? now = null)
    {
        if (!TryParsePeriod(period, out var year, out var month)) return false;
        var instant = now ?? DateTimeOffset.UtcNow;
        var startOfNextMonth = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        return instant >= startOfNextMonth;
    }

    public static string ClosedPeriodRequiredMessage(string period) =>
        $"Les primes se calculent uniquement pour un mois déjà terminé. La période « {period.Trim()} » correspond au mois en cours ou à une période future.";

    [GeneratedRegex(@"^(\d{4})-(\d{2})$")]
    private static partial Regex PeriodFormat();
}
