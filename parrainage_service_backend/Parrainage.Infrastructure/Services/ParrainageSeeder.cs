using Microsoft.EntityFrameworkCore;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Domain.Entities;

namespace Parrainage.Infrastructure.Services;

/// <summary>
/// Reproduces the localStorage demo dataset seeded by referral.service.ts /
/// admin.service.ts (DATA_VERSION 7): 15 referrals, 3 rules, derived history +
/// notifications, default notification preferences and DEFAULT_SYSTEM_CONFIG.
/// </summary>
public static class ParrainageSeeder
{
    private const long HalfDayMs = 1000L * 60 * 60 * 12;
    private const long DayMs = 86_400_000L;

    public static async Task SeedAsync(ParrainageDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Referrals.AnyAsync(ct))
        {
            logger.LogInformation("PARRAINAGE : données déjà présentes — seed ignoré.");
            await EnsureSingletonsAsync(db, ct);
            return;
        }

        logger.LogInformation("PARRAINAGE : seed du jeu de démonstration (15 parrainages + 3 règles).");
        var referrals = BuildReferrals();
        db.Referrals.AddRange(referrals);
        db.ReferralRules.AddRange(BuildRules());

        var (history, notifications) = BuildHistoryAndNotifications(referrals);
        db.ReferralHistory.AddRange(history);
        db.ReferralNotifications.AddRange(notifications);

        await EnsureSingletonsAsync(db, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : seed terminé.");
    }

    private static async Task EnsureSingletonsAsync(ParrainageDbContext db, CancellationToken ct)
    {
        if (!await db.NotificationPreferences.AnyAsync(ct))
        {
            db.NotificationPreferences.Add(new NotificationPreferenceEntity
            {
                Id = 1,
                Email = true,
                InApp = true,
                SystemAlerts = true,
                Referrals = true,
                Approvals = true,
                Payments = true,
            });
        }

        if (!await db.SystemConfigs.AnyAsync(ct))
        {
            db.SystemConfigs.Add(new SystemConfigEntity
            {
                Id = 1,
                DefaultBonusAmount = DefaultSystemConfig.DefaultBonusAmount,
                MinDurationMonths = DefaultSystemConfig.MinDurationMonths,
                ReferralLimitPerEmployee = DefaultSystemConfig.ReferralLimitPerEmployee,
                PendingReferralAlertThreshold = DefaultSystemConfig.PendingReferralAlertThreshold,
                ReferralProgramRules = DefaultSystemConfig.ProgramRules(),
                AdminWorkflow = DefaultSystemConfig.Workflow(),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static List<ReferralEntity> BuildReferrals()
    {
        var now = DateTimeOffset.UtcNow;
        var seed = new (string Id, string ReferrerId, string ReferrerName, string ProjectId, string ProjectName, string TeamId, string CandidateName, string CandidateEmail, string CandidatePhone, string Position, string Status)[]
        {
            ("ref-1001", "kyntus-employee", "Employé Démo", "proj-inbound", "Inbound grands comptes", "team-voice", "Fatima Zahra Bennis", "fatima.bennis@contactcentre.ma", "+212 6 12 34 56 78", "Agent 1er niveau", "SUBMITTED"),
            ("ref-1002", "kyntus-employee", "Employé Démo", "proj-inbound", "Inbound grands comptes", "team-voice", "Amine El Fassi", "amine.elfassi@contactcentre.ma", "+212 6 98 76 54 32", "Conseiller client", "PROCESSED"),
            ("ref-1003", "kyntus-coach", "Coach Démo", "proj-retention", "Réclamations & rétention", "team-ret", "Salma Idrissi", "salma.idrissi@contactcentre.ma", "+212 6 11 22 33 44", "Agent rétention", "REJECTED"),
            ("ref-1004", "kyntus-coach", "Coach Démo", "proj-retention", "Réclamations & rétention", "team-ret", "Youssef Alaoui", "youssef.alaoui@contactcentre.ma", "+212 6 55 66 77 88", "Conseiller client", "REWARDED"),
            ("ref-1005", "kyntus-rp", "Rp Démo", "proj-acd", "Supervision connectivité & ACD", "team-si", "Khadija Benjelloun", "khadija.benjelloun@contactcentre.ma", "+212 6 44 55 66 77", "Technicien réseau", "SUBMITTED"),
            ("ref-1006", "kyntus-yasmine", "Yasmine El Idrissi", "proj-inbound", "Inbound grands comptes", "team-chat", "Hassan Tazi", "hassan.tazi@contactcentre.ma", "+212 6 22 33 44 55", "Agent chat", "APPROVED"),
            ("ref-1007", "kyntus-superviseur", "Superviseur Démo", "proj-retention", "Réclamations & rétention", "team-ret", "Nadia Benchrif", "nadia.benchrif@contactcentre.ma", "+212 6 77 88 99 00", "Superviseur cellule", "SUBMITTED"),
            ("ref-1008", "kyntus-employee", "Employé Démo", "proj-acd", "Supervision connectivité & ACD", "team-si", "Karim Oufkir", "karim.oufkir@contactcentre.ma", "+212 6 10 20 30 40", "Superviseur ACD", "APPROVED"),
            ("ref-1009", "kyntus-rp", "Rp Démo", "proj-inbound", "Inbound grands comptes", "team-voice", "Laila Zahidi", "laila.zahidi@contactcentre.ma", "+212 6 31 41 51 61", "Coach qualité", "REJECTED"),
            ("ref-1010", "kyntus-coach", "Coach Démo", "proj-retention", "Réclamations & rétention", "team-ret", "Mehdi Chraibi", "mehdi.chraibi@contactcentre.ma", "+212 6 52 62 72 82", "Agent 1er niveau", "REWARDED"),
            ("ref-1011", "kyntus-superviseur", "Superviseur Démo", "proj-acd", "Supervision connectivité & ACD", "team-si", "Ghita Benkirane", "ghita.benkirane@contactcentre.ma", "+212 6 93 83 73 63", "Chef de projet", "SUBMITTED"),
            ("ref-1012", "kyntus-yasmine", "Yasmine El Idrissi", "proj-inbound", "Inbound grands comptes", "team-voice", "Imane Fassi", "imane.fassi@contactcentre.ma", "+212 6 12 34 56 79", "Agent 1er niveau", "SUBMITTED"),
            ("ref-1013", "kyntus-employee", "Employé Démo", "proj-retention", "Réclamations & rétention", "team-ret", "Omar Tazi", "omar.tazi@contactcentre.ma", "+212 6 14 24 34 44", "Référent technique", "APPROVED"),
            ("ref-1014", "kyntus-rp", "Rp Démo", "proj-acd", "Supervision connectivité & ACD", "team-si", "Kenza Alami", "kenza.alami@contactcentre.ma", "+212 6 15 25 35 45", "Analyste données", "IN_TRAINING"),
            ("ref-1015", "kyntus-coach", "Coach Démo", "proj-inbound", "Inbound grands comptes", "team-chat", "Hicham Benjelloun", "hicham.benjelloun@contactcentre.ma", "+212 6 16 26 36 46", "Agent chat", "IN_TRAINING"),
        };

        var list = new List<ReferralEntity>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var idx = 0; idx < seed.Length; idx++)
        {
            var r = seed[idx];
            var rewardAmount = r.Status switch
            {
                "REWARDED" => 600m + (idx % 3) * 50m,
                "IN_TRAINING" => 750m,
                _ => 0m,
            };
            var createdAt = now.AddMilliseconds(-(idx * (double)HalfDayMs + idx * (double)DayMs));
            var entity = new ReferralEntity
            {
                Id = r.Id,
                ReferrerId = r.ReferrerId,
                ReferrerName = r.ReferrerName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                TeamId = r.TeamId,
                CandidateName = r.CandidateName,
                CandidateEmail = r.CandidateEmail,
                CandidatePhone = r.CandidatePhone,
                Position = r.Position,
                AppliedRuleId = ResolveAppliedRuleId(r.Position),
                PositionMode = ResolvePositionMode(r.Position),
                Status = r.Status,
                CvUrl = ReferralCvStorageService.CvApiPath(r.Id),
                RewardAmount = rewardAmount,
                PaymentStatus = ReferralPaymentStatus.NotEligible,
                CreatedAt = createdAt,
            };

            if (r.Status == "IN_TRAINING")
            {
                entity.CandidateStartDate = today.AddMonths(-1);
                entity.TrainingEndDate = r.Id == "ref-1015" ? today.AddDays(-3) : today.AddMonths(1);
            }

            list.Add(entity);
        }

        return list;
    }

    private static string? ResolveAppliedRuleId(string position) => position switch
    {
        "Agent 1er niveau" => "rule-1",
        "Agent chat" => "rule-1",
        "Conseiller client" => "rule-2",
        "Chef de projet" => "rule-2",
        _ => null,
    };

    private static string ResolvePositionMode(string position) =>
        ResolveAppliedRuleId(position) != null ? ReferralPositionMode.Catalog : ReferralPositionMode.Custom;

    private static List<ReferralRuleEntity> BuildRules()
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            new ReferralRuleEntity { Id = "rule-1", Name = "Récompense agent inbound", Type = "REWARD_PER_POSITION", Target = "Agent 1er niveau", Value = 500, MinDurationMonths = 6, Status = "ACTIVE", CreatedAt = now.AddMilliseconds(-DayMs * 30) },
            new ReferralRuleEntity { Id = "rule-2", Name = "Récompense conseiller client", Type = "REWARD_PER_POSITION", Target = "Conseiller client", Value = 650, MinDurationMonths = 3, Status = "ACTIVE", CreatedAt = now.AddMilliseconds(-DayMs * 30) },
            new ReferralRuleEntity { Id = "rule-3", Name = "Récompense post-probatoire CC", Type = "REWARD_AFTER_PROBATION", Value = 250, MinDurationMonths = 6, Status = "PAUSED", CreatedAt = now.AddMilliseconds(-DayMs * 25) },
        };
    }

    private static (List<ReferralHistoryEntryEntity>, List<ReferralNotificationEntity>) BuildHistoryAndNotifications(
        List<ReferralEntity> referrals)
    {
        var history = new List<ReferralHistoryEntryEntity>();
        var notifications = new List<ReferralNotificationEntity>();

        foreach (var r in referrals)
        {
            var submittedAt = r.CreatedAt;
            history.Add(new ReferralHistoryEntryEntity
            {
                Id = $"hist-{r.Id}-sub",
                ReferralId = r.Id,
                CandidateName = r.CandidateName,
                Action = "SUBMITTED",
                PerformedById = r.ReferrerId,
                PerformedByLabel = r.ReferrerName,
                CreatedAt = submittedAt,
            });

            notifications.Add(new ReferralNotificationEntity
            {
                Id = $"nt-{r.Id}-sub",
                Type = "NEW_REFERRAL",
                Message = $"Nouveau parrainage : {r.CandidateName} ({r.Position}) — {r.ProjectName}",
                CreatedAt = submittedAt,
                Read = false,
                ReferralId = r.Id,
                ReferrerId = r.ReferrerId,
                TargetRoles = new() { "RH", "ADMIN", "MANAGER", "COACH", "RP" },
            });

            if (r.Status == "PROCESSED")
            {
                history.Add(new ReferralHistoryEntryEntity
                {
                    Id = $"hist-{r.Id}-proc",
                    ReferralId = r.Id,
                    CandidateName = r.CandidateName,
                    Action = "PROCESSED",
                    PerformedById = "rh-1",
                    PerformedByLabel = "RH",
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 2),
                    Comment = "Candidature examinée — attente entrée.",
                });
            }

            if (r.Status == "IN_TRAINING")
            {
                history.Add(new ReferralHistoryEntryEntity
                {
                    Id = $"hist-{r.Id}-train",
                    ReferralId = r.Id,
                    CandidateName = r.CandidateName,
                    Action = "IN_TRAINING",
                    PerformedById = "rh-1",
                    PerformedByLabel = "RH",
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 3),
                    RewardAmount = r.RewardAmount,
                    Comment = "Passage par formation.",
                });
            }

            if (r.Status is "APPROVED" or "REWARDED")
            {
                history.Add(new ReferralHistoryEntryEntity
                {
                    Id = $"hist-{r.Id}-app",
                    ReferralId = r.Id,
                    CandidateName = r.CandidateName,
                    Action = "APPROVED",
                    PerformedById = "rh-1",
                    PerformedByLabel = "RH",
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 3),
                });
            }

            if (r.Status == "REJECTED")
            {
                history.Add(new ReferralHistoryEntryEntity
                {
                    Id = $"hist-{r.Id}-rej",
                    ReferralId = r.Id,
                    CandidateName = r.CandidateName,
                    Action = "REJECTED",
                    PerformedById = "rh-1",
                    PerformedByLabel = "RH",
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 5),
                    Comment = "Profil non retenu.",
                });
            }

            if (r.Status == "REWARDED")
            {
                history.Add(new ReferralHistoryEntryEntity
                {
                    Id = $"hist-{r.Id}-rew",
                    ReferralId = r.Id,
                    CandidateName = r.CandidateName,
                    Action = "REWARDED",
                    PerformedById = "rh-1",
                    PerformedByLabel = "RH",
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 16),
                    RewardAmount = r.RewardAmount,
                });
            }
        }

        return (history, notifications);
    }
}
