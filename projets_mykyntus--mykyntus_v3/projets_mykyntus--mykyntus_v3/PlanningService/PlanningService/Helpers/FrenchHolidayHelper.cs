namespace PlanningService.Helpers;

public static class FrenchHolidayHelper
{
    // ✅ Calcule Pâques (algorithme de Meeus/Jones/Butcher)
    private static DateOnly GetEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    // ✅ Retourne tous les jours fériés français pour une année
    public static HashSet<DateOnly> GetHolidays(int year)
    {
        var easter = GetEaster(year);

        return new HashSet<DateOnly>
        {
            // Fixes
            new DateOnly(year, 1,  1),   // Jour de l'An
            new DateOnly(year, 5,  1),   // Fête du Travail
            new DateOnly(year, 5,  8),   // Victoire 1945
            new DateOnly(year, 7,  14),  // Fête Nationale
            new DateOnly(year, 8,  15),  // Assomption
            new DateOnly(year, 11, 1),   // Toussaint
            new DateOnly(year, 11, 11),  // Armistice
            new DateOnly(year, 12, 25),  // Noël

            // Basés sur Pâques
            easter.AddDays(1),           // Lundi de Pâques
            easter.AddDays(39),          // Ascension
            easter.AddDays(50),          // Lundi de Pentecôte
        };
    }

    // ✅ Vérifie si une date est un jour férié
    public static bool IsHoliday(DateOnly date)
        => GetHolidays(date.Year).Contains(date);

    // ✅ Nom du jour férié
    public static string GetHolidayName(DateOnly date)
    {
        var easter = GetEaster(date.Year);
        return date switch
        {
            _ when date == new DateOnly(date.Year, 1, 1) => "Jour de l'An",
            _ when date == new DateOnly(date.Year, 5, 1) => "Fête du Travail",
            _ when date == new DateOnly(date.Year, 5, 8) => "Victoire 1945",
            _ when date == new DateOnly(date.Year, 7, 14) => "Fête Nationale",
            _ when date == new DateOnly(date.Year, 8, 15) => "Assomption",
            _ when date == new DateOnly(date.Year, 11, 1) => "Toussaint",
            _ when date == new DateOnly(date.Year, 11, 11) => "Armistice",
            _ when date == new DateOnly(date.Year, 12, 25) => "Noël",
            _ when date == easter.AddDays(1) => "Lundi de Pâques",
            _ when date == easter.AddDays(39) => "Ascension",
            _ when date == easter.AddDays(50) => "Lundi de Pentecôte",
            _ => "Jour Férié"
        };
    }
}