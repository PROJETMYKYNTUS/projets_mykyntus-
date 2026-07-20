using System.Security.Claims;
using Kyntus.Identity.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prime.Application.Abstractions;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>
/// Crée un <c>prime_employee</c> à partir du JWT lorsque la synchro Directory/RabbitMQ
/// n’a pas encore projeté l’utilisateur (ex. après retrait du seed démo).
/// </summary>
public sealed class PrimeJwtEmployeeProvisioner(
    PrimeDbContext db,
    IConfiguration configuration,
    ILogger<PrimeJwtEmployeeProvisioner> logger)
{
    public bool IsEnabled =>
        configuration.GetValue("Prime:AutoProvisionEmployeeFromJwt", true);

    public async Task<EmployeeEntity?> TryProvisionAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!IsEnabled)
            return null;

        var email = principal.GetEmail()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var existing = await db.Employees
            .FirstOrDefaultAsync(e => e.Email.ToLower() == email, ct);
        if (existing is not null)
            return existing;

        var subjectId = principal.GetSubjectId();
        var id = subjectId is { } g && g != Guid.Empty
            ? g.ToString()
            : Guid.NewGuid().ToString();

        var role = MapAuthRoleToPrimeRole(principal.GetAuthRole());
        var (firstName, lastName) = ResolveNames(principal, email);

        var row = new EmployeeEntity
        {
            Id = id,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            PoleId = "",
        };

        try
        {
            db.Employees.Add(row);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(
                ex,
                "Échec auto-provision prime_employee pour {Email} — nouvel essai lecture.",
                email);
            db.ChangeTracker.Clear();
            return await db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email.ToLower() == email, ct);
        }

        logger.LogInformation(
            "prime_employee auto-provisionné depuis JWT : {Email} rôle={Role} id={Id}",
            email,
            role,
            id);
        return row;
    }

    private static string MapAuthRoleToPrimeRole(string? authRole)
    {
        if (string.IsNullOrWhiteSpace(authRole))
            return "Pilote";

        var r = authRole.Trim().ToLowerInvariant();
        return r switch
        {
            "admin" => "Admin",
            "rh" => "RH",
            "manager" => "Manager",
            "coach" => "Référent technique",
            "rp" => "Chef de projet",
            "audit" => "Audit",
            "employee" => "Pilote",
            "pilote" => "Pilote",
            "superviseur" => "Superviseur",
            "referent technique" or "referent_technique" => "Référent technique",
            "chef de projet" or "chef_de_projet" => "Chef de projet",
            "equipe formation" or "equipe_formation" => "RH",
            "compta" or "comptable" or "comptabilite" or "comptabilité" => "Comptabilité",
            _ => IPrimeRequestUserResolver.ExpandRole(authRole.Trim()),
        };
    }

    private static (string FirstName, string LastName) ResolveNames(ClaimsPrincipal principal, string email)
    {
        var displayName = principal.FindFirstValue(ClaimTypes.Name)?.Trim()
            ?? principal.FindFirstValue("name")?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName)
            && !displayName.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                return (parts[0], parts[1]);
            if (parts.Length == 1)
                return (parts[0], string.Empty);
        }

        var given = principal.FindFirstValue(ClaimTypes.GivenName)?.Trim()
            ?? principal.FindFirstValue("given_name")?.Trim();
        var family = principal.FindFirstValue(ClaimTypes.Surname)?.Trim()
            ?? principal.FindFirstValue("family_name")?.Trim();
        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
            return (given ?? string.Empty, family ?? string.Empty);

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
}
