using Microsoft.EntityFrameworkCore;

namespace Prime.Infrastructure;

public static class DbExceptionMessages
{
    public const string NonNegativePrimeAmountsRequired =
        "Les montants PRIME/Challenge/Total et les plafonds doivent être supérieurs ou égaux à 0.";

    public static string FromSaveChanges(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var msg = cur.Message?.Trim();
            if (string.IsNullOrEmpty(msg)) continue;
            if (msg.Contains("expected to affect", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("optimistic concurrency", StringComparison.OrdinalIgnoreCase))
                return "Les données ont été modifiées ou régénérées entre-temps. Rechargez la page puis réessayez.";
            if (msg.Contains("violates", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("23503", StringComparison.Ordinal) ||
                msg.Contains("23505", StringComparison.Ordinal) ||
                msg.Contains("23502", StringComparison.Ordinal))
                return msg;
        }

        return ex is DbUpdateException
            ? "Enregistrement refusé par la base (contrainte ou schéma). Redémarrez le backend après migration, ou vérifiez les rattachements organisationnels."
            : ex.Message;
    }
}
