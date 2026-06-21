using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;

namespace ParrainageBackend.Services;

/// <summary>Ajoute des CV placeholder pour le jeu de démo (seed + bases déjà existantes).</summary>
internal static class ParrainageDemoCvSeeder
{
    internal static async Task EnsureAsync(
        ParrainageDbContext db,
        ReferralCvStorageService cvStorage,
        ILogger logger,
        CancellationToken ct)
    {
        var referrals = await db.Referrals.ToListAsync(ct);
        if (referrals.Count == 0)
            return;

        var updated = 0;
        foreach (var referral in referrals)
        {
            var needsFile = !cvStorage.Exists(referral.Id);
            var needsUrl = string.IsNullOrWhiteSpace(referral.CvUrl);
            if (!needsFile && !needsUrl)
                continue;

            if (needsFile)
                cvStorage.EnsurePlaceholderCv(referral.Id);

            referral.CvUrl = ReferralCvStorageService.CvApiPath(referral.Id);
            updated++;
        }

        if (updated == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : CV démo provisionnés pour {Count} dossier(s).", updated);
    }
}
