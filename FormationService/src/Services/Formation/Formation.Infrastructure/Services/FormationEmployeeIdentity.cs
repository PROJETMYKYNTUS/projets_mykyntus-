using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Formation.Infrastructure.Services;

/// <summary>
/// Résout les GUIDs « équivalents » d’un acteur : JWT SubjectId + EmployeId Planning (via email annuaire).
/// Couvre le cas où les affectations utilisent le Guid Planning alors que le JWT porte un SubjectId Auth distinct.
/// </summary>
public static class FormationEmployeeIdentity
{
    public static async Task<HashSet<Guid>> ResolveAliasesAsync(
        FormationDbContext db,
        Guid jwtSubjectId,
        string? email,
        CancellationToken ct)
    {
        var set = new HashSet<Guid>();
        if (jwtSubjectId != Guid.Empty)
            set.Add(jwtSubjectId);

        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (normalized.Length > 0)
        {
            var byEmail = await db.EmployeAnnuaires.AsNoTracking()
                .Where(e => e.Email.ToLower() == normalized)
                .Select(e => e.EmployeId)
                .ToListAsync(ct);
            foreach (var id in byEmail)
            {
                if (id != Guid.Empty)
                    set.Add(id);
            }
        }

        // Si le JWT est déjà le Guid Planning, l’annuaire peut aussi porter d’autres liens email.
        if (jwtSubjectId != Guid.Empty)
        {
            var byEmployeId = await db.EmployeAnnuaires.AsNoTracking()
                .Where(e => e.EmployeId == jwtSubjectId)
                .Select(e => e.Email)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(byEmployeId) &&
                !string.Equals(byEmployeId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                var alt = byEmployeId.Trim().ToLowerInvariant();
                var siblings = await db.EmployeAnnuaires.AsNoTracking()
                    .Where(e => e.Email.ToLower() == alt)
                    .Select(e => e.EmployeId)
                    .ToListAsync(ct);
                foreach (var id in siblings)
                {
                    if (id != Guid.Empty)
                        set.Add(id);
                }
            }
        }

        return set;
    }

    /// <summary>Préfère un Guid présent dans l’annuaire (Planning), sinon le JWT.</summary>
    public static Guid PreferCanonicalEmployeeId(IReadOnlyCollection<Guid> aliases, Guid jwtSubjectId)
    {
        if (aliases.Count == 0)
            return jwtSubjectId;
        if (jwtSubjectId != Guid.Empty && aliases.Contains(jwtSubjectId) && aliases.Count == 1)
            return jwtSubjectId;
        // Prefer non-JWT alias when multiple (likely Planning Guid).
        var other = aliases.FirstOrDefault(a => a != jwtSubjectId && a != Guid.Empty);
        return other != Guid.Empty ? other : (aliases.FirstOrDefault(a => a != Guid.Empty));
    }
}
