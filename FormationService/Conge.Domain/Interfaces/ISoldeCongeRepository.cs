using Conge.Domain.Entities;
using Conge.Domain.Enums;

namespace Conge.Domain.Interfaces;

public interface ISoldeCongeRepository
{
    Task<SoldeConge?> GetByEmployeAndAnneeAsync(Guid employeId, int annee, CancellationToken ct = default);
    Task<IEnumerable<SoldeConge>> GetAllByEmployeAsync(Guid employeId, CancellationToken ct = default);
    Task AddAsync(SoldeConge solde, CancellationToken ct = default);
    void Update(SoldeConge solde);
}

public interface IEmployeSnapshotRepository
{
    Task<EmployeSnapshot?> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default);
    Task<EmployeSnapshot?> GetByEmployeIdOrEmailAsync(Guid employeId, string? email, CancellationToken ct = default);
    Task<IEnumerable<EmployeSnapshot>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeSnapshot>> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default);
    Task AddAsync(EmployeSnapshot employe, CancellationToken ct = default);
    void Update(EmployeSnapshot employe);
    void Remove(EmployeSnapshot employe);
    Task<EmployeSnapshot?> GetAdminOuRhAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid employeId, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}