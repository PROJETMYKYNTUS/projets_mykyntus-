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

        var row = new DirectoryUser
        {
            Id = userId,
            TenantId = tenantId,
            Prenom = prenom,
            Nom = nom,
            Email = email,
            Role = appRole,
            PoleId = ParseGuid(configuration["Documentation:Sync:DefaultPoleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01"),
            CelluleId = ParseGuid(configuration["Documentation:Sync:DefaultCelluleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02"),
            DepartementId = ParseGuid(configuration["Documentation:Sync:DefaultDepartementId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03"),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.DirectoryUsers.Add(row);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Annuaire documentation auto-provisionné depuis JWT : {Email} rôle={Role} id={Id}",
            email,
            appRole,
            userId);
        return row;
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
