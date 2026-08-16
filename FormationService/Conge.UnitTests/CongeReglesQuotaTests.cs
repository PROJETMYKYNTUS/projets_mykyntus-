using Conge.Application.Services;
using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using Xunit;

namespace Conge.UnitTests;

public class CongeReglesQuotaTests
{
    private sealed class FakePeriodeRepo : IPeriodeInterditeRepository
    {
        public PeriodeInterditeConge Row { get; } = PeriodeInterditeConge.CreerParDefaut();
        public Task<PeriodeInterditeConge> GetOrCreateAsync(CancellationToken ct = default) => Task.FromResult(Row);
        public void Update(PeriodeInterditeConge config) { }
    }

    private sealed class FakeQuotaRepo : IQuotaCongeServiceRepository
    {
        public QuotaCongeService? Quota { get; set; }
        public Task<QuotaCongeService?> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
            => Task.FromResult(Quota);
        public Task<IReadOnlyList<QuotaCongeService>> GetByServiceIdsAsync(IEnumerable<Guid> serviceIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<QuotaCongeService>>(Quota is null ? [] : [Quota]);
        public Task AddAsync(QuotaCongeService quota, CancellationToken ct = default) { Quota = quota; return Task.CompletedTask; }
        public void Update(QuotaCongeService quota) => Quota = quota;
    }

    private sealed class FakeDemandeRepo : IDemandeCongeRepository
    {
        public List<DemandeConge> Occupying { get; } = new();
        public Task<DemandeConge?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<DemandeConge?>(null);
        public Task<IEnumerable<DemandeConge>> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default) => Task.FromResult<IEnumerable<DemandeConge>>([]);
        public Task<IEnumerable<DemandeConge>> GetByManagerIdAsync(
            Guid managerId,
            IEnumerable<string>? validationNodeIds = null,
            CancellationToken ct = default)
            => Task.FromResult<IEnumerable<DemandeConge>>([]);
        public Task<IEnumerable<DemandeConge>> GetByStatutAsync(StatutDemande statut, CancellationToken ct = default) => Task.FromResult<IEnumerable<DemandeConge>>([]);
        public Task<IEnumerable<DemandeConge>> GetHistoriqueAsync(Guid employeId, int annee, CancellationToken ct = default) => Task.FromResult<IEnumerable<DemandeConge>>([]);
        public Task<IEnumerable<DemandeConge>> GetByAnneeAsync(int annee, CancellationToken ct = default) => Task.FromResult<IEnumerable<DemandeConge>>([]);
        public Task AddAsync(DemandeConge demande, CancellationToken ct = default) { Occupying.Add(demande); return Task.CompletedTask; }
        public void Update(DemandeConge demande) { }
        public Task<bool> ExistsCongeEnChevauchementAsync(Guid employeId, DateTime debut, DateTime fin, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<DemandeConge>> GetOccupyingQuotaAsync(
            IEnumerable<Guid> employeIds, DateTime debut, DateTime fin, Guid? excludeDemandeId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DemandeConge>>(
                Occupying.Where(d =>
                    employeIds.Contains(d.EmployeId) &&
                    CongeQuotaStatuts.Occupants.Contains(d.Statut) &&
                    d.DateDebut <= fin && d.DateFin >= debut &&
                    (!excludeDemandeId.HasValue || d.Id != excludeDemandeId.Value)).ToList());
    }

    private sealed class FakeEmployeRepo : IEmployeSnapshotRepository
    {
        public List<EmployeSnapshot> Employees { get; } = new();
        public Task<EmployeSnapshot?> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default)
            => Task.FromResult(Employees.FirstOrDefault(e => e.EmployeId == employeId));
        public Task<EmployeSnapshot?> GetByEmployeIdOrEmailAsync(Guid employeId, string? email, CancellationToken ct = default)
            => GetByEmployeIdAsync(employeId, ct);
        public Task<IEnumerable<EmployeSnapshot>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default)
            => Task.FromResult(Employees.Where(e => e.ManagerId == managerId).AsEnumerable());
        public Task<IReadOnlyList<EmployeSnapshot>> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EmployeSnapshot>>(Employees.Where(e => e.ServiceId == serviceId).ToList());
        public Task AddAsync(EmployeSnapshot employe, CancellationToken ct = default) { Employees.Add(employe); return Task.CompletedTask; }
        public void Update(EmployeSnapshot employe) { }
        public void Remove(EmployeSnapshot employe) => Employees.Remove(employe);
        public Task<EmployeSnapshot?> GetAdminOuRhAsync(CancellationToken ct = default) => Task.FromResult<EmployeSnapshot?>(null);
        public Task<bool> ExistsAsync(Guid employeId, CancellationToken ct = default) => Task.FromResult(Employees.Any(e => e.EmployeId == employeId));
    }

    [Fact]
    public async Task Quota_plein_refuse_nouvelle_periode()
    {
        var serviceId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var empRepo = new FakeEmployeRepo();
        var demandeRepo = new FakeDemandeRepo();
        var quotaRepo = new FakeQuotaRepo
        {
            Quota = QuotaCongeService.Creer(serviceId, 1)
        };

        var e1 = EmployeSnapshot.Creer(Guid.NewGuid(), "A", "A", "a@t.com", managerId, serviceId, "S", DateTime.UtcNow.AddYears(-2));
        var e2 = EmployeSnapshot.Creer(Guid.NewGuid(), "B", "B", "b@t.com", managerId, serviceId, "S", DateTime.UtcNow.AddYears(-2));
        empRepo.Employees.AddRange([e1, e2]);

        var debut = new DateTime(2026, 11, 9); // lundi futur hors mois interdits
        var fin = new DateTime(2026, 11, 11);
        var solde = SoldeConge.Initialiser(e1.EmployeId, 20, 2026);
        var existing = DemandeConge.CreerCongeAnnuel(
            e1.EmployeId, managerId, debut, fin, solde, e1,
            statutInitial: StatutDemande.EnAttenteRh);
        demandeRepo.Occupying.Add(existing);

        var regles = new CongeReglesService(new FakePeriodeRepo(), quotaRepo, demandeRepo, empRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            regles.AssertQuotaServiceDisponibleAsync(serviceId, debut, fin));
    }

    [Fact]
    public async Task EnAttente_ne_compte_pas_dans_quota()
    {
        var serviceId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var empRepo = new FakeEmployeRepo();
        var demandeRepo = new FakeDemandeRepo();
        var quotaRepo = new FakeQuotaRepo { Quota = QuotaCongeService.Creer(serviceId, 1) };

        var e1 = EmployeSnapshot.Creer(Guid.NewGuid(), "A", "A", "a@t.com", managerId, serviceId, "S", DateTime.UtcNow.AddYears(-2));
        empRepo.Employees.Add(e1);

        var debut = new DateTime(2026, 11, 16);
        var fin = new DateTime(2026, 11, 18);
        var solde = SoldeConge.Initialiser(e1.EmployeId, 20, 2026);
        var pending = DemandeConge.CreerCongeAnnuel(e1.EmployeId, managerId, debut, fin, solde, e1);
        Assert.Equal(StatutDemande.EnAttente, pending.Statut);
        demandeRepo.Occupying.Add(pending);

        var regles = new CongeReglesService(new FakePeriodeRepo(), quotaRepo, demandeRepo, empRepo);
        await regles.AssertQuotaServiceDisponibleAsync(serviceId, debut, fin); // ne throw pas
    }

    [Fact]
    public async Task Mois_interdit_bloque()
    {
        var regles = new CongeReglesService(
            new FakePeriodeRepo(), new FakeQuotaRepo(), new FakeDemandeRepo(), new FakeEmployeRepo());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            regles.AssertHorsPeriodeInterditeAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 5)));
    }
}
