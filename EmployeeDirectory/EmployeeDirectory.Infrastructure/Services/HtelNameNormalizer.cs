using System.Globalization;
using System.Text;

namespace EmployeeDirectory.Infrastructure.Services;

public static class HtelNameNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var formD = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IEnumerable<string> EmployeeNameKeys(string firstName, string lastName)
    {
        var first = Normalize(firstName);
        var last = Normalize(lastName);
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last))
            yield break;

        if (!string.IsNullOrEmpty(last) && !string.IsNullOrEmpty(first))
        {
            yield return $"{last} {first}";
            yield return $"{first} {last}";
        }
        else
        {
            yield return string.IsNullOrEmpty(last) ? first : last;
        }
    }

    public static string TechnicienKey(string technicien) => Normalize(technicien);
}
