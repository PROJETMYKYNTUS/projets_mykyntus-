namespace Conge.Domain.Entities;

/// <summary>
/// Configuration globale des mois calendaires où les congés sont interdits (récurrents chaque année).
/// Singleton logique : une seule ligne active en base.
/// </summary>
public class PeriodeInterditeConge
{
    public Guid Id { get; private set; }

    /// <summary>Mois interdits (1–12), sérialisés en JSON.</summary>
    public string MoisInterditsJson { get; private set; } = "[9,10]";

    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private PeriodeInterditeConge() { }

    public static PeriodeInterditeConge CreerParDefaut()
    {
        return new PeriodeInterditeConge
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0001"),
            MoisInterditsJson = "[9,10]",
            UpdatedAt = DateTime.UtcNow
        };
    }

    public IReadOnlyList<int> GetMois()
    {
        try
        {
            var months = System.Text.Json.JsonSerializer.Deserialize<int[]>(MoisInterditsJson)
                ?? Array.Empty<int>();
            return months
                .Where(m => m is >= 1 and <= 12)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
        }
        catch
        {
            return new List<int> { 9, 10 };
        }
    }

    public void MettreAJour(IEnumerable<int> mois, Guid? updatedBy = null)
    {
        var cleaned = mois
            .Where(m => m is >= 1 and <= 12)
            .Distinct()
            .OrderBy(m => m)
            .ToArray();
        MoisInterditsJson = System.Text.Json.JsonSerializer.Serialize(cleaned);
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public bool ChevauchePeriode(DateTime debut, DateTime fin)
    {
        var mois = GetMois();
        if (mois.Count == 0) return false;

        var d = debut.Date;
        var f = fin.Date;
        if (f < d) (d, f) = (f, d);

        for (var day = d; day <= f; day = day.AddDays(1))
        {
            if (mois.Contains(day.Month))
                return true;
        }

        return false;
    }
}
