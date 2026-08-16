using Conge.Domain.Entities;
using Conge.Domain.Enums;

namespace Conge.Domain.Interfaces;

public interface IDemandeCongeRepository
{
    Task<DemandeConge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default);
    /// <summary>
    /// File manager : filtrer par <paramref name="managerId"/> (compat)
    /// et/ou par <paramref name="validationNodeIds"/> (périmètre multi-responsables).
    /// </summary>
    Task<IEnumerable<DemandeConge>> GetByManagerIdAsync(
        Guid managerId,
        IEnumerable<string>? validationNodeIds = null,
        CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByStatutAsync(StatutDemande statut, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetHistoriqueAsync(Guid employeId, int annee, CancellationToken ct = default);
    Task<IEnumerable<DemandeConge>> GetByAnneeAsync(int annee, CancellationToken ct = default);
    Task AddAsync(DemandeConge demande, CancellationToken ct = default);

    void Update(DemandeConge demande);
    Task<bool> ExistsCongeEnChevauchementAsync(Guid employeId, DateTime debut, DateTime fin, CancellationToken ct = default);

    /// <summary>
    /// Congés occupant le quota (EnAttenteRh / Validee) pour les employés des services donnés,
    /// chevauchant [debut, fin].
    /// </summary>
    Task<IReadOnlyList<DemandeConge>> GetOccupyingQuotaAsync(
        IEnumerable<Guid> employeIds,
        DateTime debut,
        DateTime fin,
        Guid? excludeDemandeId = null,
        CancellationToken ct = default);
}