using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

public sealed record EmployeeDirectoryUpsertRequest(
    Guid EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? PrimeServiceId = null,
    Guid SupervisorId = default,
    bool SkipOrgStructureFields = false);

public interface IEmployeeDirectorySyncService
{
    Task<string> UpsertAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default);
    Task<string> EnsureFromPlanningAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default);
    Task<int> DedupeByEmailAsync(CancellationToken ct = default);
}

public sealed class EmployeeDirectorySyncService(
    PrimeDbContext db,
    ILogger<EmployeeDirectorySyncService> logger) : IEmployeeDirectorySyncService
{
    public Task<string> UpsertAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default) =>
        UpsertCoreAsync(request, matchByEmail: true, forcePlanningId: false, ct);

    public Task<string> EnsureFromPlanningAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default) =>
        UpsertCoreAsync(request, matchByEmail: true, forcePlanningId: true, ct);

    public static EmployeeDirectoryUpsertRequest FromEmployeMessage(EmployeCreatedMessage msg) =>
        new(
            EmployeeId: msg.EmployeId,
            FirstName: msg.Prenom,
            LastName: msg.Nom,
            Email: msg.Email,
            Role: msg.Role,
            PrimeServiceId: msg.PrimeServiceId,
            SupervisorId: msg.SupervisorId);

    public static EmployeeDirectoryUpsertRequest FromEmployeMessage(EmployeUpdatedMessage msg) =>
        new(
            EmployeeId: msg.EmployeId,
            FirstName: msg.Prenom,
            LastName: msg.Nom,
            Email: msg.Email,
            Role: msg.Role,
            PrimeServiceId: msg.PrimeServiceId,
            SupervisorId: msg.SupervisorId,
            SkipOrgStructureFields: msg.SkipOrgStructureFields);

    public async Task<int> DedupeByEmailAsync(CancellationToken ct = default)
    {
        var groups = await db.Employees
            .GroupBy(e => e.Email.ToLower())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(ct);

        var merged = 0;
        foreach (var emailKey in groups)
        {
            var rows = await db.Employees.Where(e => e.Email.ToLower() == emailKey).OrderBy(e => e.Id).ToListAsync(ct);
            if (rows.Count < 2) continue;

            var canonical = rows.FirstOrDefault(e => Guid.TryParse(e.Id, out _)) ?? rows[0];
            foreach (var dup in rows.Where(r => r.Id != canonical.Id))
            {
                MergeEmployeeFields(canonical, dup);
                db.Employees.Remove(dup);
                merged++;
            }
        }

        if (merged > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dedupe employés : {Count} doublon(s) fusionné(s)", merged);
        }

        return merged;
    }

    private async Task<string> UpsertCoreAsync(
        EmployeeDirectoryUpsertRequest request,
        bool matchByEmail,
        bool forcePlanningId,
        CancellationToken ct)
    {
        var id = request.EmployeeId.ToString();
        var role = NormalizeRole(request.Role);
        var email = request.Email.Trim();

        var (serviceId, celluleId, poleId) = await ResolveOrgIdsAsync(request.PrimeServiceId, ct);

        EmployeeEntity? existing = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null && matchByEmail && !string.IsNullOrWhiteSpace(email))
            existing = await db.Employees.FirstOrDefaultAsync(e => e.Email.ToLower() == email.ToLower(), ct);

        if (existing is null)
        {
            db.Employees.Add(new EmployeeEntity
            {
                Id = id,
                Email = email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = role,
                ServiceId = serviceId,
                CelluleId = celluleId,
                PoleId = poleId ?? "",
                ParentId = request.SupervisorId != Guid.Empty ? request.SupervisorId.ToString() : null
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("prime_employee créé {Email} id={Id} rôle={Role}", email, id, role);
            return id;
        }

        if (forcePlanningId && !string.Equals(existing.Id, id, StringComparison.Ordinal))
        {
            var merged = new EmployeeEntity
            {
                Id = id,
                Email = email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = ShouldPreserveStructure(existing, request) ? existing.Role : role,
                ServiceId = ShouldPreserveStructure(existing, request) ? existing.ServiceId : serviceId ?? existing.ServiceId,
                CelluleId = ShouldPreserveStructure(existing, request) ? existing.CelluleId : celluleId ?? existing.CelluleId,
                PoleId = ShouldPreserveStructure(existing, request) ? existing.PoleId : poleId ?? existing.PoleId,
                ParentId = request.SupervisorId != Guid.Empty ? request.SupervisorId.ToString() : existing.ParentId,
                Avatar = existing.Avatar
            };
            db.Employees.Remove(existing);
            db.Employees.Add(merged);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("prime_employee fusionné {Email} id={OldId}→{NewId}", email, existing.Id, id);
            return id;
        }

        existing.Email = email;
        existing.FirstName = request.FirstName;
        existing.LastName = request.LastName;
        if (!ShouldPreserveStructure(existing, request))
        {
            existing.Role = role;
            existing.ServiceId = serviceId ?? existing.ServiceId;
            existing.CelluleId = celluleId ?? existing.CelluleId;
            existing.PoleId = poleId ?? existing.PoleId;
        }
        if (request.SupervisorId != Guid.Empty)
            existing.ParentId = request.SupervisorId.ToString();

        await db.SaveChangesAsync(ct);
        logger.LogInformation("prime_employee synchronisé {Email} id={Id} rôle={Role}", email, existing.Id, existing.Role);
        return existing.Id;
    }

    private static bool ShouldPreserveStructure(EmployeeEntity existing, EmployeeDirectoryUpsertRequest request)
    {
        if (request.SkipOrgStructureFields)
            return HasActiveStructureAssignment(existing);
        return HasActiveStructureAssignment(existing);
    }

    internal static bool HasActiveStructureAssignment(EmployeeEntity e) =>
        (KyntusRoleNames.IsChefDeProjet(e.Role)
         || KyntusRoleNames.IsSuperviseur(e.Role)
         || KyntusRoleNames.IsReferentTechnique(e.Role))
        && (!string.IsNullOrWhiteSpace(e.PoleId)
            || !string.IsNullOrWhiteSpace(e.CelluleId)
            || !string.IsNullOrWhiteSpace(e.ServiceId));

    private static void MergeEmployeeFields(EmployeeEntity target, EmployeeEntity source)
    {
        if (string.IsNullOrWhiteSpace(target.FirstName)) target.FirstName = source.FirstName;
        if (string.IsNullOrWhiteSpace(target.LastName)) target.LastName = source.LastName;
        if (HasActiveStructureAssignment(source) && !HasActiveStructureAssignment(target))
        {
            target.Role = source.Role;
            target.PoleId = source.PoleId;
            target.CelluleId = source.CelluleId;
            target.ServiceId = source.ServiceId;
            target.ParentId = source.ParentId;
        }
    }

    private async Task<(string? ServiceId, string? CelluleId, string? PoleId)> ResolveOrgIdsAsync(
        string? primeServiceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primeServiceId))
            return (null, null, null);

        var svc = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == primeServiceId, ct);
        if (svc is null)
            return (primeServiceId, null, null);

        var celluleId = svc.CelluleId;
        var cell = await db.Cellules.AsNoTracking().FirstOrDefaultAsync(c => c.Id == celluleId, ct);
        return (primeServiceId, celluleId, cell?.PoleId);
    }

    private static string NormalizeRole(string role)
    {
        var normalized = KyntusRoleNames.NormalizePlanningRole(role);
        if (string.Equals(normalized, KyntusRoleNames.Manager, StringComparison.OrdinalIgnoreCase))
            return KyntusRoleNames.Superviseur;
        return normalized;
    }
}
