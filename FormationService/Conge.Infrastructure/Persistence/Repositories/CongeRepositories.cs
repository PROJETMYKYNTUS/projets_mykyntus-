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
        => await _context.DemandeConges
            .Include(d => d.Decisions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IEnumerable<DemandeConge>> GetByEmployeIdAsync(Guid employeId, CancellationToken ct = default)
        => await _context.DemandeConges
            .Include(d => d.Decisions)
            .Where(d => d.EmployeId == employeId)
            .OrderByDescending(d => d.DateDemande)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetByManagerIdAsync(
        Guid managerId,
        IEnumerable<string>? validationNodeIds = null,
        CancellationToken ct = default)
    {
        var nodes = validationNodeIds?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var q = _context.DemandeConges.Include(d => d.Decisions).AsQueryable();
        if (nodes.Count > 0)
        {
            q = q.Where(d =>
                d.ManagerId == managerId
                || (d.ValidationNodeId != null && nodes.Contains(d.ValidationNodeId)));
        }
        else
        {
            q = q.Where(d => d.ManagerId == managerId);
        }

        return await q.OrderByDescending(d => d.DateDemande).ToListAsync(ct);
    }

    public async Task<IEnumerable<DemandeConge>> GetByStatutAsync(StatutDemande statut, CancellationToken ct = default)
        => await _context.DemandeConges
            .Include(d => d.Decisions)
            .Where(d => d.Statut == statut)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetHistoriqueAsync(Guid employeId, int annee, CancellationToken ct = default)
        => await _context.DemandeConges
            .Include(d => d.Decisions)
            .Where(d => d.EmployeId == employeId && d.DateDebut.Year == annee)
            .OrderByDescending(d => d.DateDemande)
            .ToListAsync(ct);

    public async Task<IEnumerable<DemandeConge>> GetByAnneeAsync(int annee, CancellationToken ct = default)
        => await _context.DemandeConges
            .Include(d => d.Decisions)
            .Where(d => d.DateDebut.Year == annee || d.DateFin.Year == annee)
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

    public async Task<IReadOnlyList<DemandeConge>> GetOccupyingQuotaAsync(
        IEnumerable<Guid> employeIds,
        DateTime debut,
        DateTime fin,
        Guid? excludeDemandeId = null,
        CancellationToken ct = default)
    {
        var ids = employeIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<DemandeConge>();

        var occupants = CongeQuotaStatuts.Occupants;
        var q = _context.DemandeConges.Where(d =>
            ids.Contains(d.EmployeId) &&
            occupants.Contains(d.Statut) &&
            d.DateDebut <= fin &&
            d.DateFin >= debut);

        if (excludeDemandeId.HasValue)
            q = q.Where(d => d.Id != excludeDemandeId.Value);

        return await q.ToListAsync(ct);
    }
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

    public async Task<EmployeSnapshot?> GetByEmployeIdOrEmailAsync(
        Guid employeId,
        string? email,
        CancellationToken ct = default)
    {
        var row = await GetByEmployeIdAsync(employeId, ct);
        if (row is not null)
            return row;

        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        return await _context.EmployeSnapshots
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalized, ct);
    }

    public async Task<IEnumerable<EmployeSnapshot>> GetByManagerIdAsync(Guid managerId, CancellationToken ct = default)
        => await _context.EmployeSnapshots
            .Where(e => e.ManagerId == managerId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmployeSnapshot>> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
        => await _context.EmployeSnapshots
            .Where(e => e.ServiceId == serviceId)
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

    public void Remove(EmployeSnapshot employe)
        => _context.EmployeSnapshots.Remove(employe);

    public async Task<bool> ExistsAsync(Guid employeId, CancellationToken ct = default)
        => await _context.EmployeSnapshots.AnyAsync(e => e.EmployeId == employeId, ct);
}

public class PeriodeInterditeRepository : IPeriodeInterditeRepository
{
    private readonly CongeDbContext _context;

    public PeriodeInterditeRepository(CongeDbContext context) => _context = context;

    public async Task<PeriodeInterditeConge> GetOrCreateAsync(CancellationToken ct = default)
    {
        var row = await _context.PeriodesInterdites.FirstOrDefaultAsync(ct);
        if (row is not null) return row;

        row = PeriodeInterditeConge.CreerParDefaut();
        await _context.PeriodesInterdites.AddAsync(row, ct);
        await _context.SaveChangesAsync(ct);
        return row;
    }

    public void Update(PeriodeInterditeConge config)
        => _context.PeriodesInterdites.Update(config);
}

public class QuotaCongeServiceRepository : IQuotaCongeServiceRepository
{
    private readonly CongeDbContext _context;

    public QuotaCongeServiceRepository(CongeDbContext context) => _context = context;

    public async Task<QuotaCongeService?> GetByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
        => await _context.QuotasCongeService.FirstOrDefaultAsync(q => q.ServiceId == serviceId, ct);

    public async Task<IReadOnlyList<QuotaCongeService>> GetByServiceIdsAsync(
        IEnumerable<Guid> serviceIds,
        CancellationToken ct = default)
    {
        var ids = serviceIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<QuotaCongeService>();
        return await _context.QuotasCongeService
            .Where(q => ids.Contains(q.ServiceId))
            .ToListAsync(ct);
    }

    public async Task AddAsync(QuotaCongeService quota, CancellationToken ct = default)
        => await _context.QuotasCongeService.AddAsync(quota, ct);

    public void Update(QuotaCongeService quota)
        => _context.QuotasCongeService.Update(quota);
}