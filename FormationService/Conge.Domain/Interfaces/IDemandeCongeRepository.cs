using Conge.Domain.Entities;
using Conge.Domain.Enums;

namespace Conge.Domain.Interfaces;

public interface IDemandeCongeRepository
{
    Task<DemandeConge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByStatutAsync(StatutDemande statut, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetHistoriqueAsync(Guid employeId, int annee, CancellationToken ct = default);
    Task AddAsync(DemandeConge demande, CancellationToken ct = default);

    void Update(DemandeConge demande);
    Task<bool> ExistsCongeEnChevauchementAsync(Guid employeId, DateTime debut, DateTime fin, CancellationToken ct = default);
}