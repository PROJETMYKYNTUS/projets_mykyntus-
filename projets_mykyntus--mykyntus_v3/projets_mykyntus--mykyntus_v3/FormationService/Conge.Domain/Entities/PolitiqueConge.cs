using Conge.Domain.Enums;

namespace Conge.Domain.Entities;

/// <summary>
/// Politique de congés conforme au Code du travail marocain (loi 65-99, articles 231-250).
/// </summary>
public class PolitiqueConge
{
    // ── Congé annuel (art. 231-243) ──────────────────────────────────────────
    public const double JoursParMoisAdulte = 1.5;
    public const double JoursParMoisMineur = 2.0;
    public const int JoursMinimumConsecutifs = 12;
    public const int JoursMaximumAnnuel = 30;
    public const int MoisAncienneteMinimum = 6;
    public const double JoursBonusAnciennetePar5Ans = 1.5;
    public const int PreavisEmployeurJours = 30;

    // ── Congés exceptionnels ─────────────────────────────────────────────────
    public static readonly Dictionary<TypeCongeExceptionnel, int> DureesExceptionnelles = new()
    {
        { TypeCongeExceptionnel.Mariage,       4 },
        { TypeCongeExceptionnel.DecesConjoint, 3 },
        { TypeCongeExceptionnel.DecesParent,   2 },
        { TypeCongeExceptionnel.Naissance,     3 },  // Paternité
        { TypeCongeExceptionnel.Maternite,     98 }  // 14 semaines
    };

    /// <summary>
    /// Calcule le solde annuel selon l'ancienneté et l'âge (art. 231, 240).
    /// </summary>
    public static double CalculerSoldeAnnuel(int ancienneteAnnees, bool estMineur)
    {
        double joursBase = estMineur
            ? JoursParMoisMineur * 12
            : JoursParMoisAdulte * 12;

        // +1.5 jour tous les 5 ans d'ancienneté
        double bonusAnciennete = Math.Floor(ancienneteAnnees / 5.0) * JoursBonusAnciennetePar5Ans;

        return Math.Min(joursBase + bonusAnciennete, JoursMaximumAnnuel);
    }

    /// <summary>
    /// Retourne la durée d'un congé exceptionnel selon l'événement.
    /// </summary>
    public static int GetDureeExceptionnelle(TypeCongeExceptionnel type)
    {
        return DureesExceptionnelles.TryGetValue(type, out var duree) ? duree : 0;
    }

    /// <summary>
    /// Vérifie si une demande de congé annuel respecte les 12 jours consécutifs minimum.
    /// </summary>
    public static bool RespecteDureeMinimaleConsecutive(DateTime debut, DateTime fin)
    {
        var joursOuvrables = CompterJoursOuvrables(debut, fin);
        return joursOuvrables >= JoursMinimumConsecutifs;
    }

    /// <summary>
    /// Compte les jours ouvrables (hors weekends) entre deux dates.
    /// Les jours fériés marocains ne sont pas décomptés du congé (art. 243).
    /// </summary>
    public static int CompterJoursOuvrables(DateTime debut, DateTime fin)
    {
        int jours = 0;
        for (var d = debut.Date; d <= fin.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                jours++;
        }
        return jours;
    }
}