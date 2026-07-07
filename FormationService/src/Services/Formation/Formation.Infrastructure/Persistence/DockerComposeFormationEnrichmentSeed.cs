using Formation.Domain.Entities;
using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Persistence;

/// <summary>
/// Catalogue formations centre d'appels Casablanca + inscriptions pour démo Docker.
/// </summary>
public static class DockerComposeFormationEnrichmentSeed
{
    private const string MarkerTitre = "Accueil nouveaux agents inbound";

    private static readonly (Guid EmployeId, string Nom)[] DemoEmployes =
    [
        (Guid.Parse("11111111-1111-4111-8111-111111111103"), "Employé Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111101"), "Yasmine El Amrani"),
        (Guid.Parse("11111111-1111-4111-8111-111111111106"), "Coach Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111107"), "Rp Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111111"), "Superviseur Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111105"), "Manager Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111110"), "Formation Démo"),
        (Guid.Parse("11111111-1111-4111-8111-111111111104"), "Rh Démo"),
    ];

    public static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        FormationDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        if (await db.Formations.AnyAsync(f => EF.Functions.Like(f.Titre, "%Accueil nouveaux agents inbound%"), ct))
        {
            logger?.LogInformation("Formation enrichment déjà appliqué.");
            return;
        }

        var now = DateTime.UtcNow;

        var specs = new (string Titre, string Desc, string Formateur, DateTime Debut, DateTime Fin, int Cap, (Guid, string)[]? Inscrits)[]
        {
            ("Accueil nouveaux agents inbound — grands comptes",
                "Parcours d'intégration voice/chat pour la plateforme inbound Casablanca (démo).",
                "Latifa Mansouri", now.AddDays(5), now.AddDays(7), 25, DemoEmployes.Take(8).ToArray()),
            ("Qualité & NPS — rappels satisfaction",
                "Techniques de rappel et scripts NPS pour enquêtes satisfaction (démo).",
                "Omar Tazi", now.AddDays(12), now.AddDays(13), 20, DemoEmployes.Take(5).ToArray()),
            ("Procédures réclamations & rétention",
                "Workflow réclamations et offres de rétention — cellule Casablanca (démo).",
                "Kenza Alami", now.AddDays(18), now.AddDays(19), 15, DemoEmployes.Take(3).ToArray()),
            ("Soft skills — relation client marocaine",
                "Communication et gestion de conflit — contexte marocain (démo).",
                "Hicham Benjelloun", now.AddDays(25), now.AddDays(26), 30, null),
            ("Supervision connectivité & ACD (historique)",
                "Session passée — supervision télécom (démo).",
                "Nadia Benchrif", now.AddDays(-30), now.AddDays(-28), 12, null),
            ("Formation rétention — historique",
                "Session passée — techniques de rétention (démo).",
                "Ghita Benkirane", now.AddDays(-14), now.AddDays(-13), 10, null),
        };

        var created = 0;
        foreach (var spec in specs)
        {
            var formation = CreateValidated(spec.Titre, spec.Desc, spec.Formateur, spec.Debut, spec.Fin, spec.Cap, 0);
            db.Formations.Add(formation);
            await db.SaveChangesAsync(ct);

            if (spec.Inscrits is { Length: > 0 })
            {
                foreach (var (employeId, nom) in spec.Inscrits)
                    formation.Inscrire(employeId, nom);
                await db.SaveChangesAsync(ct);
            }

            created++;
        }

        logger?.LogInformation("Formation enrichment : {Count} formations créées.", created);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_FORMATION_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    private static FormationEntity CreateValidated(
        string titre,
        string description,
        string formateur,
        DateTime debut,
        DateTime fin,
        int capacite,
        decimal prix)
    {
        var f = FormationEntity.Create(titre, description, formateur, debut, fin, capacite, prix);
        f.Valider();
        return f;
    }

    private static void InscrireEmployes(FormationEntity formation, (Guid EmployeId, string Nom)[] employes)
    {
        foreach (var (employeId, nom) in employes)
            formation.Inscrire(employeId, nom);
    }
}
