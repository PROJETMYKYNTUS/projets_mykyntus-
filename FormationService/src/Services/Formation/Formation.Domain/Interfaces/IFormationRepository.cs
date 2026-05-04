using Formation.Domain.Entities;
using Formation.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formation.Domain.Interfaces;

public interface IFormationRepository
{
    Task<FormationEntity?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<FormationEntity>> GetAllAsync(StatutFormation? statut, CancellationToken ct);
    Task AddAsync(FormationEntity formation, CancellationToken ct);
    void Update(FormationEntity formation);
    void Delete(FormationEntity formation);
    Task SaveChangesAsync(CancellationToken ct);
}
public interface IInscriptionRepository
{
    Task<Entities.Inscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Entities.Inscription>> GetByFormationAsync(Guid formationId, CancellationToken ct = default);
    Task<List<Entities.Inscription>> GetByEmployeAsync(Guid employeId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}