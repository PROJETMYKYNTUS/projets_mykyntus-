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
    Guid SupervisorId = default);

public interface IEmployeeDirectorySyncService
{
    /// <summary>Upsert employé (Planning → Prime) avec recherche par id ou email (flux RabbitMQ).</summary>
    Task<string> UpsertAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default);

    /// <summary>Garantit un employé Prime avec <c>Id = EmployeeId</c> (flux synchrone Gestion employés).</summary>
    Task<string> EnsureFromPlanningAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default);
}

public sealed class EmployeeDirectorySyncService(
    PrimeDbContext db,
    PrimeInMemoryStore store,
    ILogger<EmployeeDirectorySyncService> logger) : IEmployeeDirectorySyncService
{
    public Task<string> UpsertAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default) =>
        UpsertCoreAsync(request, matchByEmail: true, forcePlanningId: false, ct);

    public Task<string> EnsureFromPlanningAsync(EmployeeDirectoryUpsertRequest request, CancellationToken ct = default) =>
        UpsertCoreAsync(request, matchByEmail: false, forcePlanningId: true, ct);

    public static EmployeeDirectoryUpsertRequest FromEmployeMessage(EmployeCreatedMessage msg) =>
        new(
            EmployeeId: msg.EmployeId,
            FirstName: msg.Prenom,
            LastName: msg.Nom,
            Email: msg.Email,
            Role: msg.Role,
            PrimeServiceId: msg.PrimeServiceId,
            SupervisorId: msg.SupervisorId);

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
            existing = await db.Employees.FirstOrDefaultAsync(e => e.Email == email, ct);

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
        }
        else
        {
            if (forcePlanningId && !string.Equals(existing.Id, id, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "ensure-from-planning: employé email={Email} id={ExistingId} ignoré — création id={PlanningId}",
                    email,
                    existing.Id,
                    id);
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
            }
            else
            {
                existing.Email = email;
                existing.FirstName = request.FirstName;
                existing.LastName = request.LastName;
                existing.Role = role;
                existing.ServiceId = serviceId ?? existing.ServiceId;
                existing.CelluleId = celluleId ?? existing.CelluleId;
                existing.PoleId = poleId ?? existing.PoleId;
                if (request.SupervisorId != Guid.Empty)
                    existing.ParentId = request.SupervisorId.ToString();
            }
        }

        await db.SaveChangesAsync(ct);
        store.HydrateOrganizationFromDatabase(db);
        logger.LogInformation("prime_employee synchronisé {Email} id={Id} rôle={Role}", email, id, role);
        return id;
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
