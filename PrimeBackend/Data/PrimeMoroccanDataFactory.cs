using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bogus;

namespace PrimeBackend.Data;

/// <summary>Génération Bogus reproductible — contexte centre de contact marocain.</summary>
public sealed class PrimeMoroccanDataFactory
{
    public const int DefaultSeed = 42;
    public const string DefaultEmailDomain = "contactcentre.ma";
    public const string EnrichEmployeeIdPrefix = "emp-ma-";

    private readonly Faker _faker;
    private readonly string _emailDomain;

    public PrimeMoroccanDataFactory(int seed = DefaultSeed, string? emailDomain = null)
    {
        Randomizer.Seed = new Random(seed);
        _faker = new Faker("fr");
        _emailDomain = string.IsNullOrWhiteSpace(emailDomain) ? DefaultEmailDomain : emailDomain.Trim().ToLowerInvariant();
    }

    public Faker Faker => _faker;

    public (string FirstName, string LastName, string Email) Person(string? emailDomainOverride = null)
    {
        var fn = _faker.Name.FirstName();
        var ln = _faker.Name.LastName();
        var domain = emailDomainOverride ?? _emailDomain;
        var email = $"{Slug(fn)}.{Slug(ln)}@{domain}";
        return (fn, ln, email);
    }

    public string NewEnrichEmployeeId() => $"{EnrichEmployeeIdPrefix}{Guid.NewGuid():N}";

    public (decimal Prime, decimal Challenge) Amounts(int salt = 0)
    {
        var r = new Random(DefaultSeed + salt * 17);
        var prime = (decimal)r.Next(800, 2501);
        var challenge = (decimal)r.Next(150, 801);
        return (prime, challenge);
    }

    public string PerformanceJson(int salt = 0)
    {
        var r = new Random(DefaultSeed + salt * 7);
        var completed = r.Next(8, 18);
        var total = completed + r.Next(2, 6);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            completedTasks = completed,
            totalTasks = total,
            objectivesReached = r.Next(2, 6),
            totalObjectives = r.Next(4, 9),
            nps = r.Next(35, 92),
            site = PickSite(r),
            monthlyScores = new[]
            {
                new { month = "Jan", score = r.Next(65, 95) },
                new { month = "Fév", score = r.Next(65, 95) },
                new { month = "Mar", score = r.Next(65, 95) },
                new { month = "Avr", score = r.Next(65, 95) },
            },
        });
    }

    public string RejectionReason() =>
        _faker.PickRandom(
            "Écart entre appels traités (ACD) et saisie manuelle des indicateurs — resynchroniser.",
            "NPS hors plage contractuelle pour la période — correction requise avant validation.",
            "Grille challenge incomplète sur le service — compléter la saisie pilote.",
            "Délai de traitement réclamation non justifié par les tickets CRM.");

    public string AuditNote(string? city = null)
    {
        var cityPart = city ?? PickSite(new Random(DefaultSeed + _faker.Random.Int()));
        return $"{_faker.Lorem.Sentence(5)} ({cityPart}).";
    }

    public string AnomalyDescription(string type, string period, string? serviceName = null)
    {
        var svc = string.IsNullOrWhiteSpace(serviceName) ? "service" : serviceName;
        return type switch
        {
            "ComputationMismatch" => $"Montant prime incohérent avec la grille {svc} pour {period}.",
            "StaleValidation" => $"Fiche {period} en attente depuis plus de 72 h — relancer le validateur.",
            "OutOfRange" => $"Indicateur NPS hors bornes contractuelles ({period}, {svc}).",
            "MissingApprover" => $"Statut avancé sans approbateur enregistré ({period}).",
            "DuplicateFiche" => $"Doublon potentiel sur {period} pour {svc}.",
            "InvalidScope" => $"Périmètre organisationnel incohérent pour {period}.",
            "WorkflowBlocked" => $"Transition workflow bloquée sur la période {period}.",
            _ => $"Anomalie détectée sur {period} ({svc}).",
        };
    }

    public static string EmailDomainFromPoleName(string poleName)
    {
        var slug = Slug(poleName);
        if (string.IsNullOrEmpty(slug)) return DefaultEmailDomain;
        if (slug.Length > 24) slug = slug[..24].TrimEnd('-');
        return $"{slug}.ma";
    }

    public static IReadOnlyList<(string Id, string Label, decimal PrimePct, decimal ChallengePct)> IndicatorSet(int serviceIndex) =>
        IndicatorSets[serviceIndex % IndicatorSets.Length];

    private static readonly (string Id, string Label, decimal PrimePct, decimal ChallengePct)[][] IndicatorSets =
    [
        [
            ("nps-agents", "NPS agents (%)", 30m, 20m),
            ("aht-voice", "AHT voice (sec)", 25m, 15m),
            ("qa-score", "Score QA écoutes", 25m, 25m),
            ("fcr", "First contact resolution (%)", 20m, 20m),
        ],
        [
            ("nps-enquetes", "NPS enquêtes sortantes", 35m, 25m),
            ("taux-rappel", "Taux de rappel abouti", 30m, 20m),
            ("csat", "CSAT post-appel", 35m, 30m),
        ],
        [
            ("taux-retention", "Taux rétention client", 40m, 30m),
            ("delai-traitement", "Délai traitement réclamation (h)", 30m, 20m),
            ("engagements-tenus", "Engagements tenus (%)", 30m, 25m),
        ],
        [
            ("dispo-acd", "Disponibilité ACD (%)", 35m, 25m),
            ("incidents-p1", "Incidents P1 résolus < 4h", 35m, 30m),
            ("mttr", "MTTR réseau (min)", 30m, 20m),
        ],
    ];

    private static string PickSite(Random r) =>
        new[] { "Casablanca", "Rabat", "Oujda", "Tanger", "Marrakech" }[r.Next(0, 5)];

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "collaborateur";
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var s = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "collaborateur" : s;
    }
}
