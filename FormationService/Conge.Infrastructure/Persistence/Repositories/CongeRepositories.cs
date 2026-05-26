using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Conge.Infrastructure.Persistence.Repositories;

public class DemandeCongeRepository : IDemandeCongeRepository
{
    private readonly CongeDbContext _context;

    public DemandeCongeRepository(CongeDbContext context) => _context = context;

    public async Task<DemandeConge?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.DemandeConges.FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<DemandeConge>> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default)
        => await _context.DemandeConges
            .Where(d => d.EmployeId == employeId)
            .OrderByDescending(d => d.DateDemande)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default)
        => await _context.DemandeConges
            .Where(d => d.ManagerId == managerId)
            .OrderByDescending(d => d.DateDemande)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetByStatutAsync(StatutDemande statut, CancellationToken ct = default)
        => await _context.DemandeConges
            .Where(d => d.Statut == statut)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetHistoriqueAsync(Guid employeId, int annee, CancellationToken ct = default)
        => await _context.DemandeConges
            .Where(d => d.EmployeId == employeId && d.DateDebut.Year == annee)
            .OrderByDescending(d => d.DateDemande)
            .ToListAsync(ct);

    public async Task AddAsync(DemandeConge demande, CancellationToken ct = default)
        => await _context.DemandeConges.AddAsync(demande, ct);

    public void Update(DemandeConge demande)
        => _context.DemandeConges.Update(demande);
    public async Task<bool> ExistsCongeEnChevauchementAsync(Guid employeId, DateTime debut, DateTime fin, CancellationToken ct = default)
        => await _context.DemandeConges.AnyAsync(d =>
            d.EmployeId == employeId &&
            d.Statut != StatutDemande.Refusee &&
            d.Statut != StatutDemande.Annulee &&
            d.DateDebut <= fin && d.DateFin >= debut, ct);
}

public class SoldeCongeRepository : ISoldeCongeRepository
{
    private readonly CongeDbContext _context;

    public SoldeCongeRepository(CongeDbContext context) => _context = context;

    public async Task<SoldeConge?> GetByEmployeAndAnneeAsync(Guid employeId, int annee, CancellationToken ct = default)
        => await _context.SoldeConges
            .FirstOrDefaultAsync(s => s.EmployeId == employeId && s.Annee == annee, ct);

    public async Task<IEnumerable<SoldeConge>> GetAllByEmployeAsync(Guid employeId, CancellationToken ct = default)
        => await _context.SoldeConges
            .Where(s => s.EmployeId == employeId)
            .OrderByDescending(s => s.Annee)
            .ToListAsync(ct);

    public async Task AddAsync(SoldeConge solde, CancellationToken ct = default)
        => await _context.SoldeConges.AddAsync(solde, ct);

    public void Update(SoldeConge solde)
        => _context.SoldeConges.Update(solde);
}

public class EmployeSnapshotRepository : IEmployeSnapshotRepository
{
    private readonly CongeDbContext _context;

    public EmployeSnapshotRepository(CongeDbContext context) => _context = context;

    public async Task<EmployeSnapshot?> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default)
        => await _context.EmployeSnapshots
            .FirstOrDefaultAsync(e => e.EmployeId == employeId, ct);

    public async Task<IEnumerable<EmployeSnapshot>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default)
        => await _context.EmployeSnapshots
            .Where(e => e.ManagerId == managerId)
            .ToListAsync(ct);
    public async Task<EmployeSnapshot?> GetAdminOuRhAsync(CancellationToken ct = default)
    {
        return await _context.EmployeSnapshots
            .Where(e => e.Role == "Admin" || e.Role == "RH")
            .OrderBy(e => e.Role) // Admin en premier
            .FirstOrDefaultAsync(ct);
    }
    public async Task AddAsync(EmployeSnapshot employe, CancellationToken ct = default)
        => await _context.EmployeSnapshots.AddAsync(employe, ct);

    public void Update(EmployeSnapshot employe)
        => _context.EmployeSnapshots.Update(employe);

    public async Task<bool> ExistsAsync(Guid employeId, CancellationToken ct = default)
        => await _context.EmployeSnapshots.AnyAsync(e => e.EmployeId == employeId, ct);
}