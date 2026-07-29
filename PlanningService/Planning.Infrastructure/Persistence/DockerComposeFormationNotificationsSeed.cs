using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Notifications cloche « Formation » pour utilisateurs Planning **déjà présents**.
/// Event-only : ne crée / ne modifie aucun User.
/// </summary>
internal static class DockerComposeFormationNotificationsSeed
{
    private const string MarkerWeekCode = "TRAINING-SEED-MARK";

    private static readonly Guid FormateurKyntusGuid = Guid.Parse("11111111-1111-4111-8111-111111111110");
    private static readonly Guid FormateurGmailGuid = Guid.Parse("11111111-1111-4111-8111-111111111120");
    private static readonly Guid EmployeeGuid = Guid.Parse("11111111-1111-4111-8111-111111111103");
    private static readonly Guid CoachGuid = Guid.Parse("11111111-1111-4111-8111-111111111106");

    internal static async Task ApplyIfEnabledAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Planning.FormationNotificationsSeed");

        if (await context.PlanningNotifications.AnyAsync(n => n.WeekCode == MarkerWeekCode, ct))
        {
            logger.LogInformation("Notifications formation déjà seedées.");
            return;
        }

        var formateurKyntus = await ResolveExistingUserAsync(context, FormateurKyntusGuid, "formation@kyntus.ma", logger, ct);
        var formateurGmail = await ResolveExistingUserAsync(context, FormateurGmailGuid, "formateur@gmail.com", logger, ct);
        var employee = await ResolveExistingUserAsync(context, EmployeeGuid, "employee@kyntus.ma", logger, ct);
        var coach = await ResolveExistingUserAsync(context, CoachGuid, "coach@kyntus.ma", logger, ct);

        if (formateurKyntus is null && formateurGmail is null && employee is null && coach is null)
        {
            logger.LogWarning("Notifications formation ignorées : aucun destinataire Planning existant.");
            return;
        }

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

        var markerUser = formateurGmail ?? formateurKyntus ?? employee ?? coach;
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
        logger.LogInformation("Notifications formation : {Count} ligne(s) créées (event-only).", added);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "false", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<User?> ResolveExistingUserAsync(
        AppDbContext context,
        Guid guid,
        string email,
        ILogger logger,
        CancellationToken ct)
    {
        var needle = email.ToLowerInvariant();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Guid == guid && u.IsActive, ct)
            ?? await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == needle && u.IsActive, ct);

        if (user is null)
            logger.LogWarning("Notifications formation : utilisateur {Email}/{Guid} absent — skip.", email, guid);

        return user;
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
