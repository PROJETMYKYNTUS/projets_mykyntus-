using Conge.Domain.Entities;
using Conge.Domain.Interfaces;

namespace Conge.Application.Services;

public record CongeDisponibiliteResult(
    bool Ok,
    string? Motif,
    IReadOnlyList<int> MoisInterdits,
    IReadOnlyList<string> JoursSatures);

/// <summary>
/// Règles période interdite + quota service (absents simultanés).
/// </summary>
public class CongeReglesService
{
    private readonly IPeriodeInterditeRepository _periodeRepo;
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;

    public CongeReglesService(
        IPeriodeInterditeRepository periodeRepo,
        IQuotaCongeServiceRepository quotaRepo,
        IDemandeCongeRepository demandeRepo,
        IEmployeSnapshotRepository employeRepo)
    {
        _periodeRepo = periodeRepo;
        _quotaRepo = quotaRepo;
        _demandeRepo = demandeRepo;
        _employeRepo = employeRepo;
    }

    public async Task AssertHorsPeriodeInterditeAsync(DateTime debut, DateTime fin, CancellationToken ct = default)
    {
        var config = await _periodeRepo.GetOrCreateAsync(ct);
        if (!config.ChevauchePeriode(debut, fin))
            return;

        var mois = config.GetMois();
        var labels = string.Join(", ", mois.Select(MoisLabel));
        throw new InvalidOperationException(
            $"La période chevauche un mois interdit aux congés ({labels}).");
    }

    public async Task AssertQuotaServiceDisponibleAsync(
        Guid serviceId,
        DateTime debut,
        DateTime fin,
        Guid? excludeDemandeId = null,
        CancellationToken ct = default)
    {
        if (serviceId == Guid.Empty)
            return;

        var quota = await _quotaRepo.GetByServiceIdAsync(serviceId, ct);
        if (quota is null)
            return; // pas de limite configurée

        var employes = await _employeRepo.GetByServiceIdAsync(serviceId, ct);
        var employeIds = employes.Select(e => e.EmployeId).ToList();
        if (employeIds.Count == 0)
            return;

        var occupying = await _demandeRepo.GetOccupyingQuotaAsync(
            employeIds, debut, fin, excludeDemandeId, ct);

        var saturated = FindSaturatedDays(debut, fin, occupying, quota.MaxAbsentsSimultanes);
        if (saturated.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Quota service atteint le {saturated[0]:dd/MM/yyyy} " +
            $"(max {quota.MaxAbsentsSimultanes} absent(s) simultané(s)).");
    }

    public async Task<CongeDisponibiliteResult> EvaluerDisponibiliteAsync(
        Guid employeId,
        DateTime debut,
        DateTime fin,
        CancellationToken ct = default)
    {
        var employe = await _employeRepo.GetByEmployeIdAsync(employeId, ct);
        var config = await _periodeRepo.GetOrCreateAsync(ct);
        var mois = config.GetMois().ToList();

        if (config.ChevauchePeriode(debut, fin))
        {
            var labels = string.Join(", ", mois.Select(MoisLabel));
            return new CongeDisponibiliteResult(
                false,
                $"La période chevauche un mois interdit aux congés ({labels}).",
                mois,
                Array.Empty<string>());
        }

        if (employe is null || employe.ServiceId == Guid.Empty)
            return new CongeDisponibiliteResult(true, null, mois, Array.Empty<string>());

        var quota = await _quotaRepo.GetByServiceIdAsync(employe.ServiceId, ct);
        if (quota is null)
            return new CongeDisponibiliteResult(true, null, mois, Array.Empty<string>());

        var peers = await _employeRepo.GetByServiceIdAsync(employe.ServiceId, ct);
        var occupying = await _demandeRepo.GetOccupyingQuotaAsync(
            peers.Select(e => e.EmployeId), debut, fin, null, ct);

        var saturated = FindSaturatedDays(debut, fin, occupying, quota.MaxAbsentsSimultanes);
        if (saturated.Count == 0)
            return new CongeDisponibiliteResult(true, null, mois, Array.Empty<string>());

        return new CongeDisponibiliteResult(
            false,
            $"Quota service atteint le {saturated[0]:dd/MM/yyyy} (max {quota.MaxAbsentsSimultanes} absent(s)).",
            mois,
            saturated.Select(d => d.ToString("yyyy-MM-dd")).ToList());
    }

    public async Task<IReadOnlyList<int>> GetMoisInterditsAsync(CancellationToken ct = default)
    {
        var config = await _periodeRepo.GetOrCreateAsync(ct);
        return config.GetMois();
    }

    private static List<DateTime> FindSaturatedDays(
        DateTime debut,
        DateTime fin,
        IReadOnlyList<DemandeConge> occupying,
        int maxAbsents)
    {
        var d = debut.Date;
        var f = fin.Date;
        if (f < d) (d, f) = (f, d);

        var saturated = new List<DateTime>();
        for (var day = d; day <= f; day = day.AddDays(1))
        {
            var count = occupying.Count(c => c.DateDebut.Date <= day && c.DateFin.Date >= day);
            if (count >= maxAbsents)
                saturated.Add(day);
        }

        return saturated;
    }

    private static string MoisLabel(int m) => m switch
    {
        1 => "janvier",
        2 => "février",
        3 => "mars",
        4 => "avril",
        5 => "mai",
        6 => "juin",
        7 => "juillet",
        8 => "août",
        9 => "septembre",
        10 => "octobre",
        11 => "novembre",
        12 => "décembre",
        _ => $"mois {m}"
    };
}
