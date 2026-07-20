using System.Security.Claims;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Persistence;
using Kyntus.Identity.Jwt;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Documentation.Infrastructure.Services;

/// <summary>
/// Crée ou met à jour un profil annuaire documentation à partir du JWT lorsque la synchro RabbitMQ n'a pas encore eu lieu.
/// </summary>
public sealed class DocumentationJwtDirectoryProvisioner(
    DocumentationDbContext db,
    IConfiguration configuration,
    ILogger<DocumentationJwtDirectoryProvisioner> logger)
{
    public async Task<DirectoryUser?> TryProvisionAsync(
        ClaimsPrincipal principal,
        string tenantId,
        CancellationToken ct)
    {
        var email = principal.GetEmail()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var existing = await db.DirectoryUsers
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email.ToLower() == email, ct);
        if (existing is not null)
            return existing;

        var roleName = KyntusPortalRoleMapping.ToDocumentationRoleName(principal.GetAuthRole());
        if (!Enum.TryParse<AppRole>(roleName, ignoreCase: true, out var appRole))
            appRole = AppRole.Pilote;

        var userId = principal.GetSubjectId() ?? Guid.NewGuid();
        var (prenom, nom) = ResolveNames(principal, email);
        var now = DateTimeOffset.UtcNow;

        var poleId = ParseGuid(configuration["Documentation:Sync:DefaultPoleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01");
        var celluleId = ParseGuid(configuration["Documentation:Sync:DefaultCelluleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02");
        var departementId = ParseGuid(configuration["Documentation:Sync:DefaultDepartementId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03");

        // Sans unités org, l'insert directory_users échoue en FK 23503 → HTTP 500 sur /users/me et document-requests.
        await EnsureDefaultOrganisationUnitsAsync(tenantId, poleId, celluleId, departementId, now, ct);

        var row = new DirectoryUser
        {
            Id = userId,
            TenantId = tenantId,
            Prenom = prenom,
            Nom = nom,
            Email = email,
            Role = appRole,
            PoleId = poleId,
            CelluleId = celluleId,
            DepartementId = departementId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            db.DirectoryUsers.Add(row);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(
                ex,
                "Échec auto-provision annuaire documentation pour {Email} — nouvel essai lecture.",
                email);
            db.ChangeTracker.Clear();
            return await db.DirectoryUsers
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email.ToLower() == email, ct);
        }

        logger.LogInformation(
            "Annuaire documentation auto-provisionné depuis JWT : {Email} rôle={Role} id={Id}",
            email,
            appRole,
            userId);
        return row;
    }

    /// <summary>
    /// Garantit les 3 unités org par défaut (pôle → cellule → département) utilisées par le provisionnement JWT.
    /// </summary>
    private async Task EnsureDefaultOrganisationUnitsAsync(
        string tenantId,
        Guid poleId,
        Guid celluleId,
        Guid departementId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await EnsureOrgUnitAsync(tenantId, poleId, null, "pole", "DEFAULT-POLE", "Pôle par défaut", now, ct);
        await EnsureOrgUnitAsync(tenantId, celluleId, poleId, "cellule", "DEFAULT-CELLULE", "Cellule par défaut", now, ct);
        await EnsureOrgUnitAsync(tenantId, departementId, celluleId, "departement", "DEFAULT-DEPT", "Département par défaut", now, ct);

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    private async Task EnsureOrgUnitAsync(
        string tenantId,
        Guid id,
        Guid? parentId,
        string unitType,
        string code,
        string name,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var exists = await db.OrganisationUnits
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id == id, ct);
        if (exists)
            return;

        db.OrganisationUnits.Add(new OrganisationUnit
        {
            Id = id,
            TenantId = tenantId,
            ParentId = parentId,
            UnitType = unitType,
            Code = code,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    private static (string Prenom, string Nom) ResolveNames(ClaimsPrincipal principal, string email)
    {
        var displayName = principal.FindFirstValue(ClaimTypes.Name)?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName) && !displayName.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                return (parts[0], parts[1]);
            if (parts.Length == 1)
                return (parts[0], string.Empty);
        }

        var local = email.Split('@')[0];
        var dotParts = local.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (dotParts.Length == 2)
            return (Capitalize(dotParts[0]), Capitalize(dotParts[1]));

        return (Capitalize(local), string.Empty);
    }

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static Guid ParseGuid(string? value, string fallback) =>
        Guid.TryParse(value, out var g) ? g : Guid.Parse(fallback);
}
