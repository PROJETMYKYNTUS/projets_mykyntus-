using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

/// <summary>
/// Rattrapage idempotent : soumission Pending pour toutes les fiches prêtes (brouillon Validated + cellule Complete).
/// Aucun pilote ni période en dur.
/// </summary>
public static class PrimeValidationDemoRepair
{
    public sealed record Result(
        int DraftsValidated,
        int FichesEnsured,
        int ReconciledGlobal,
        int ReconciledByPeriod);

    public static Task<Result> ApplyAsync(
        PrimeDbContext db,
        PrimeFicheValidationSubmissionService submission,
        ILogger logger,
        CancellationToken ct = default) =>
        ReconcileCoreAsync(db, submission, logger, ct);

    public static Task<Result> ReconcileOnlyAsync(
        PrimeDbContext db,
        PrimeFicheValidationSubmissionService submission,
        ILogger logger,
        CancellationToken ct = default) =>
        ReconcileCoreAsync(db, submission, logger, ct);

    private static async Task<Result> ReconcileCoreAsync(
        PrimeDbContext db,
        PrimeFicheValidationSubmissionService submission,
        ILogger logger,
        CancellationToken ct)
    {
        var syncedDrafts = await submission.SyncAllValidatedDraftsAsync(ct);
        var global = await submission.ReconcileReadySubmissionsAsync(ct);
        var byPeriod = 0;
        var periods = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Select(f => f.Period)
            .Distinct()
            .OrderByDescending(p => p)
            .Take(24)
            .ToListAsync(ct);
        foreach (var period in periods)
            byPeriod += await submission.ReconcileReadySubmissionsForPeriodAsync(period, ct);

        if (syncedDrafts > 0 || global > 0 || byPeriod > 0)
            logger.LogInformation(
                "PRIME validation reconcile : brouillons synchronisés={SyncedDrafts}, reconcile global={Global}, par période={ByPeriod}",
                syncedDrafts,
                global,
                byPeriod);

        return new Result(0, 0, global, byPeriod);
    }
}
