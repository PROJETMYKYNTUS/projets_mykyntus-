using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Notifications cloche « Formation » (PlanningNotifications) pour formateurs et bénéficiaires.
/// Les seeds Formation créent des sessions sans publier MassTransit — ce seed comble le gap.
/// </summary>
internal static class DockerComposeFormationNotificationsSeed
{
    private const string MarkerWeekCode = "TRAINING-SEED-MARK";

    private static readonly Guid FormateurKyntusGuid = Guid.Parse("11111111-1111-4111-8111-111111111110");
    private static readonly Guid FormateurGmailGuid = Guid.Parse("11111111-1111-4111-8111-111111111120");
    private static readonly Guid EmployeeGuid = Guid.Parse("11111111-1111-4111-8111-111111111103");
    private static readonly Guid CoachGuid = Guid.Parse("11111111-1111-4111-8111-111111111106");
    private static readonly Guid SuperviseurGuid = Guid.Parse("11111111-1111-4111-8111-111111111111");

    internal static async Task ApplyIfEnabledAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Planning.FormationNotificationsSeed");

        if (await context.PlanningNotifications.AnyAsync(n => n.WeekCode == MarkerWeekCode, ct))
        {
            logger.LogInformation("Notifications formation déjà seedées.");
            return;
        }

        await EnsureRolesAsync(context, ct);

        var pwd = configuration["DemoSeed:PlanningDemoPassword"] ?? "Azerty@123";
        var hire = DateTime.UtcNow.AddMonths(-6);

        await UpsertUserAsync(context, FormateurKyntusGuid, "Hicham", "Benjelloun", "formation@kyntus.ma", "EquipeFormation", null, pwd, hire, ct);
        await UpsertUserAsync(context, FormateurGmailGuid, "Formateur", "Demo", "formateur@gmail.com", "EquipeFormation", null, pwd, hire, ct);
        await UpsertUserAsync(context, EmployeeGuid, "Yasmine", "El Idrissi", "employee@kyntus.ma", "Pilote", null, pwd, hire, ct);
        await UpsertUserAsync(context, CoachGuid, "Omar", "Tazi", "coach@kyntus.ma", "Référent technique", null, pwd, hire, ct);
        await UpsertUserAsync(context, SuperviseurGuid, "Kenza", "Alami", "superviseur@kyntus.ma", "Superviseur", null, pwd, hire, ct);
        await context.SaveChangesAsync(ct);

        try
        {
            await userService.SyncMissingAuthUsersAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sync Auth pour notifications formation partielle.");
        }

        // Recharger après sync Auth
        var formateurKyntus = await context.Users.FirstOrDefaultAsync(u => u.Guid == FormateurKyntusGuid, ct);
        var formateurGmail = await context.Users.FirstOrDefaultAsync(u => u.Guid == FormateurGmailGuid, ct);
        var employee = await context.Users.FirstOrDefaultAsync(u => u.Guid == EmployeeGuid, ct);
        var coach = await context.Users.FirstOrDefaultAsync(u => u.Guid == CoachGuid, ct);

        var now = DateTime.UtcNow;
        var added = 0;

        added += await TryAddAsync(context, formateurGmail, "TRAINING-ANIM-DEMO01",
            "Vous êtes animateur de la formation « Atelier formateur — rituels d'accueil » (début prochain).",
            now.AddHours(-2), ct);
        added += await TryAddAsync(context, formateurGmail, "TRAINING-START-ANIM-01",
            "La formation « Coaching qualité — file formateur (en cours) » a démarré — ouvrez l'appel des présences.",
            now.AddHours(-1), ct);
        added += await TryAddAsync(context, formateurGmail, "TRAINING-ANIM-DEMO03",
            "Nouveau parcours initiale à traiter — Sara Bennani (file formateur).",
            now.AddMinutes(-40), ct);

        added += await TryAddAsync(context, formateurKyntus, "TRAINING-ANIM-DEMO02",
            "Vous êtes animateur de la formation « Atelier formateur — onboarding inbound ».",
            now.AddHours(-3), ct);
        added += await TryAddAsync(context, formateurKyntus, "TRAINING-START-ANIM-02",
            "La formation « Coaching qualité — équipe formation (en cours) » a démarré — ouvrez l'appel des présences.",
            now.AddMinutes(-90), ct);

        added += await TryAddAsync(context, employee, "TRAINING-DEMO-ASN-01",
            "Vous êtes inscrit à la formation « Atelier formateur — rituels d'accueil » (début prochain).",
            now.AddHours(-4), ct);
        added += await TryAddAsync(context, employee, "TRAINING-START-BEN-01",
            "La formation « Coaching qualité — file formateur (en cours) » a démarré.",
            now.AddMinutes(-50), ct);

        added += await TryAddAsync(context, coach, "TRAINING-DEMO-ASN-02",
            "Vous êtes inscrit à la formation « Qualité softphone — Agents 1er niveau ».",
            now.AddDays(-1), ct);

        // Marqueur d'idempotence (sur le premier destinataire disponible)
        var markerUser = formateurGmail ?? formateurKyntus ?? employee;
        if (markerUser?.AuthUserId is int authId)
        {
            context.PlanningNotifications.Add(new PlanningNotification
            {
                UserId = markerUser.Id,
                AuthUserId = authId,
                WeekCode = MarkerWeekCode,
                SubServiceName = "Formation continue",
                Message = "Seed notifications formation appliqué.",
                IsRead = true,
                CreatedAt = now.AddDays(-30),
                ReadAt = now.AddDays(-30),
            });
            added++;
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Notifications formation : {Count} ligne(s) créées.", added);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureRolesAsync(AppDbContext context, CancellationToken ct)
    {
        foreach (var (name, desc) in new (string, string)[]
                 {
                     ("Pilote", "Pilote"),
                     ("EquipeFormation", "Équipe formation"),
                     ("Référent technique", "Référent technique"),
                     ("Superviseur", "Superviseur"),
                 })
        {
            if (await context.Roles.AnyAsync(r => r.Name == name, ct))
                continue;
            context.Roles.Add(new Role { Name = name, Description = desc, IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task UpsertUserAsync(
        AppDbContext context,
        Guid guid,
        string first,
        string last,
        string email,
        string roleName,
        int? subServiceId,
        string password,
        DateTime hire,
        CancellationToken ct)
    {
        var role = await context.Roles.FirstAsync(r => r.Name == roleName, ct);
        var needle = email.ToLowerInvariant();
        var row = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == needle, ct)
            ?? await context.Users.FirstOrDefaultAsync(u => u.Guid == guid, ct);

        if (row is null)
        {
            context.Users.Add(new User
            {
                Guid = guid,
                FirstName = first,
                LastName = last,
                Email = email,
                RoleId = role.Id,
                SubServiceId = subServiceId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                HireDate = hire,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            return;
        }

        row.Guid = guid;
        row.FirstName = first;
        row.LastName = last;
        row.Email = email;
        row.RoleId = role.Id;
        row.IsActive = true;
        if (string.IsNullOrWhiteSpace(row.PasswordHash))
            row.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static async Task<int> TryAddAsync(
        AppDbContext context,
        User? user,
        string weekCode,
        string message,
        DateTime createdAt,
        CancellationToken ct)
    {
        if (user?.AuthUserId is not int authId)
            return 0;

        if (await context.PlanningNotifications.AnyAsync(
                n => n.AuthUserId == authId && n.WeekCode == weekCode, ct))
            return 0;

        context.PlanningNotifications.Add(new PlanningNotification
        {
            UserId = user.Id,
            AuthUserId = authId,
            WeeklyPlanningId = null,
            WeekCode = weekCode,
            SubServiceName = "Formation continue",
            Message = message,
            IsRead = false,
            CreatedAt = createdAt,
        });
        return 1;
    }
}
