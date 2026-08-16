namespace Planning.Application.Abstractions;

/// <summary>
/// Synchronise les plannings existants après validation / refus de congé :
/// mise à jour chirurgicale (IsOnLeave) ou régénération si règles / couverture cassées.
/// </summary>
public interface IPlanningLeaveImpactService
{
    /// <param name="absenceRemoved">true après refus/annulation (retrait d'absence).</param>
    Task SyncAfterAbsenceChangeAsync(
        int userId,
        DateOnly start,
        DateOnly end,
        bool absenceRemoved,
        CancellationToken ct = default);
}
