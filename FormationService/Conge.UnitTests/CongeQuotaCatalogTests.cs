using Conge.Application.Abstractions;
using Conge.Application.Commands.ConfigConge;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Xunit;

namespace Conge.UnitTests;

public class CongeQuotaCatalogTests
{
    private sealed class FakeQuotaRepo : IQuotaCongeServiceRepository
    {
        public Task<QuotaCongeService?> GetByServiceIdAsync(string serviceId, CancellationToken ct = default)
            => Task.FromResult<QuotaCongeService?>(null);
        public Task<IReadOnlyList<QuotaCongeService>> GetByServiceIdsAsync(IEnumerable<string> serviceIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<QuotaCongeService>>([]);
        public Task AddAsync(QuotaCongeService quota, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(QuotaCongeService quota) { }
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
        public Task<IReadOnlyList<EmployeSnapshot>> GetByPerimeterAsync(
            Guid managerId, IReadOnlyList<string>? orgNodeIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EmployeSnapshot>>(
                Employees.Where(e => e.ManagerId == managerId ||
                    (orgNodeIds is not null && e.CelluleId is not null && orgNodeIds.Contains(e.CelluleId))).ToList());
        public Task<IReadOnlyList<EmployeSnapshot>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EmployeSnapshot>>(Employees.ToList());
        public Task<IReadOnlyList<EmployeSnapshot>> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
            => GetByOrgNodeIdAsync(serviceId.ToString(), ct);
        public Task<IReadOnlyList<EmployeSnapshot>> GetByOrgNodeIdAsync(string orgNodeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EmployeSnapshot>>(
                Employees.Where(e => e.CelluleId == orgNodeId || e.OrgServiceId == orgNodeId).ToList());
        public Task AddAsync(EmployeSnapshot employe, CancellationToken ct = default) { Employees.Add(employe); return Task.CompletedTask; }
        public void Update(EmployeSnapshot employe) { }
        public void Remove(EmployeSnapshot employe) => Employees.Remove(employe);
        public Task<EmployeSnapshot?> GetAdminOuRhAsync(CancellationToken ct = default) => Task.FromResult<EmployeSnapshot?>(null);
        public Task<bool> ExistsAsync(Guid employeId, CancellationToken ct = default) => Task.FromResult(Employees.Any(e => e.EmployeId == employeId));
    }

    private sealed class FakeCatalog : IDirectoryOrgCatalog
    {
        private readonly DirectoryOrgCatalogSnapshot _snap;
        public FakeCatalog(DirectoryOrgCatalogSnapshot snap) => _snap = snap;
        public Task<DirectoryOrgCatalogSnapshot> GetSnapshotAsync(CancellationToken ct = default) => Task.FromResult(_snap);
        public Task<string?> ResolveNodeNameAsync(string nodeId, CancellationToken ct = default)
            => Task.FromResult(_snap.GetName(nodeId));
    }

    [Fact]
    public async Task GetQuotas_utilise_noms_et_effectifs_Directory()
    {
        const string celluleId = "cell-abc123def456";
        var superviseurId = Guid.NewGuid();
        var empRepo = new FakeEmployeRepo();
        empRepo.Employees.Add(EmployeSnapshot.Creer(
            superviseurId, "Sup", "S", "s@t.com", Guid.Empty, Guid.Empty, "",
            DateTime.UtcNow.AddYears(-3), false, "Superviseur", null, celluleId, null));
        // Un agent rattaché (effectif snapshot = 1) mais Directory dit 4.
        empRepo.Employees.Add(EmployeSnapshot.Creer(
            Guid.NewGuid(), "A", "A", "a@t.com", superviseurId, Guid.Empty, "cell-abc123def456",
            DateTime.UtcNow.AddYears(-1), false, "Employee", null, celluleId, null));

        var catalog = new FakeCatalog(new DirectoryOrgCatalogSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [celluleId] = "Préparation RDV"
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [celluleId] = 4
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));

        var handler = new GetQuotasServiceHandler(empRepo, new FakeQuotaRepo(), catalog, rebac: null);
        var rows = await handler.Handle(new GetQuotasServiceQuery(superviseurId), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(celluleId, row.ServiceId);
        Assert.Equal("Préparation RDV", row.ServiceNom);
        Assert.Equal(4, row.Effectif);
        Assert.Equal(QuotaScopeKinds.Cellule, row.ScopeKind);
    }

    [Fact]
    public void ResolveNodeLabel_ignore_ServiceNom_qui_est_un_id()
    {
        const string celluleId = "cell-xyz789abc012";
        var e = EmployeSnapshot.Creer(
            Guid.NewGuid(), "A", "A", "a@t.com", Guid.NewGuid(), Guid.Empty, celluleId,
            DateTime.UtcNow.AddYears(-1), false, "Employee", null, celluleId, null);

        var label = CongeQuotaPerimeter.ResolveNodeLabel(e, celluleId, QuotaScopeKinds.Cellule);
        Assert.StartsWith("Cellule ", label);

        var catalog = new DirectoryOrgCatalogSnapshot(
            new Dictionary<string, string> { [celluleId] = "Cellule Nord" },
            new Dictionary<string, int>(),
            new Dictionary<string, int>());
        var nice = CongeQuotaPerimeter.ResolveNodeLabel(e, celluleId, QuotaScopeKinds.Cellule, catalog);
        Assert.Equal("Cellule Nord", nice);
    }
}
