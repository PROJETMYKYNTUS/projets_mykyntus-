using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Infrastructure;

public static class DbExceptionMessages
{
    public static string FromSaveChanges(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var msg = cur.Message?.Trim();
            if (string.IsNullOrEmpty(msg)) continue;
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
