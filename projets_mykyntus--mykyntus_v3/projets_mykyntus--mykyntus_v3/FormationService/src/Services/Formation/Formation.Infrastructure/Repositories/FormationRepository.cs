using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Domain.Interfaces;
using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Formation.Infrastructure.Repositories;

public class FormationRepository : IFormationRepository
{
    private readonly FormationDbContext _context;
    public FormationRepository(FormationDbContext context) => _context = context;

    public async Task<FormationEntity?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _context.Formations
            .Include(f => f.Inscriptions)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<List<FormationEntity>> GetAllAsync(StatutFormation? statut, CancellationToken ct)
    {
        var query = _context.Formations.Include(f => f.Inscriptions).AsQueryable();
        if (statut.HasValue) query = query.Where(f => f.Statut == statut.Value);
        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(FormationEntity formation, CancellationToken ct)
        => await _context.Formations.AddAsync(formation, ct);

    public void Update(FormationEntity formation)
        => _context.Formations.Update(formation);

    public void Delete(FormationEntity formation)
        => _context.Formations.Remove(formation);
    public async Task AddInscriptionAsync(Inscription inscription, CancellationToken ct)
    => await _context.Inscriptions.AddAsync(inscription, ct);
    public async Task SaveChangesAsync(CancellationToken ct)
        => await _context.SaveChangesAsync(ct);
}