namespace Planning.Application.Common;

/// <summary>Résout une période prédéfinie (ce mois, mois dernier, …) en bornes de dates.</summary>
public static class PeriodRange
{
    public static (DateOnly? From, DateOnly? To) Resolve(
        string? period,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (from.HasValue || to.HasValue)
            return (from, to);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        return (period ?? "all").Trim().ToLowerInvariant() switch
        {
            "thismonth" or "ce_mois" => (firstOfMonth, null),
            "lastmonth" or "mois_dernier" => (
                firstOfMonth.AddMonths(-1),
                firstOfMonth.AddDays(-1)),
            "last3months" or "3_mois" => (firstOfMonth.AddMonths(-2), null),
            "thisyear" or "annee" => (new DateOnly(today.Year, 1, 1), null),
            "all" or "tout" or "" => (null, null),
            _ => (null, null)
        };
    }
}
