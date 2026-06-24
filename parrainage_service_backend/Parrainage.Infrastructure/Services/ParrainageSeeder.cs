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
            ("ref-1001", "emp-1", "Jean Dupont", "proj-1", "Alpha Digital", "team-a", "Claire Martin", "claire.martin@email.com", "+33 6 12 34 56 78", "Développeur Full-Stack", "SUBMITTED"),
            ("ref-1002", "emp-1", "Jean Dupont", "proj-1", "Alpha Digital", "team-a", "Paul Bernard", "paul.bernard@email.com", "+33 6 98 76 54 32", "Chef de projet", "PROCESSED"),
            ("ref-1003", "emp-2", "Sophie Leroy", "proj-2", "Beta Ops", "team-b", "Luc Petit", "luc.petit@email.com", "+33 6 11 22 33 44", "Analyste data", "REJECTED"),
            ("ref-1004", "emp-2", "Sophie Leroy", "proj-2", "Beta Ops", "team-b", "Nadia Kaci", "nadia.kaci@email.com", "+33 6 55 66 77 88", "Développeur", "REWARDED"),
            ("ref-1005", "emp-3", "Thomas Bernard", "proj-3", "Gamma Cloud", "team-c", "Amélie Rousseau", "amelie.rousseau@email.com", "+33 6 44 55 66 77", "DevOps", "SUBMITTED"),
            ("ref-1006", "emp-4", "Julie Moreau", "proj-1", "Alpha Digital", "team-a", "Hugo Garnier", "hugo.garnier@email.com", "+33 6 22 33 44 55", "Développeur", "APPROVED"),
            ("ref-1007", "emp-5", "Karim Benali", "proj-2", "Beta Ops", "team-b", "Sarah Cohen", "sarah.cohen@email.com", "+33 6 77 88 99 00", "Chef de produit", "SUBMITTED"),
            ("ref-1008", "emp-1", "Jean Dupont", "proj-3", "Gamma Cloud", "team-c", "Marc Lefèvre", "marc.lefevre@email.com", "+33 6 10 20 30 40", "Architecte SI", "APPROVED"),
            ("ref-1009", "emp-3", "Thomas Bernard", "proj-1", "Alpha Digital", "team-a", "Élodie Vincent", "elodie.vincent@email.com", "+33 6 31 41 51 61", "Designer UX", "REJECTED"),
            ("ref-1010", "emp-4", "Julie Moreau", "proj-2", "Beta Ops", "team-b", "Nicolas Faure", "nicolas.faure@email.com", "+33 6 52 62 72 82", "Développeur", "REWARDED"),
            ("ref-1011", "emp-5", "Karim Benali", "proj-3", "Gamma Cloud", "team-c", "Inès Hadj", "ines.hadj@email.com", "+33 6 93 83 73 63", "Scrum master", "SUBMITTED"),
            ("ref-1012", "emp-2", "Sophie Leroy", "proj-1", "Alpha Digital", "team-a", "Claire Martin", "claire.martin@email.com", "+33 6 12 34 56 79", "Développeur", "SUBMITTED"),
            ("ref-1013", "emp-1", "Jean Dupont", "proj-2", "Beta Ops", "team-b", "Antoine Dupuis", "antoine.dupuis@email.com", "+33 6 14 24 34 44", "Lead développement", "APPROVED"),
            ("ref-1014", "emp-3", "Thomas Bernard", "proj-3", "Gamma Cloud", "team-c", "Léa Marchand", "lea.marchand@email.com", "+33 6 15 25 35 45", "Ingénieure données", "IN_TRAINING"),
            ("ref-1015", "emp-4", "Julie Moreau", "proj-1", "Alpha Digital", "team-a", "Youssef Alami", "youssef.alami@email.com", "+33 6 16 26 36 46", "Développeur", "IN_TRAINING"),
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
        "Développeur" => "rule-1",
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
            new ReferralRuleEntity { Id = "rule-1", Name = "Récompense Développeur", Type = "REWARD_PER_POSITION", Target = "Développeur", Value = 600, MinDurationMonths = 6, Status = "ACTIVE", CreatedAt = now.AddMilliseconds(-DayMs * 30) },
            new ReferralRuleEntity { Id = "rule-2", Name = "Récompense Chef de projet", Type = "REWARD_PER_POSITION", Target = "Chef de projet", Value = 750, MinDurationMonths = 3, Status = "ACTIVE", CreatedAt = now.AddMilliseconds(-DayMs * 30) },
            new ReferralRuleEntity { Id = "rule-3", Name = "Récompense post-probatoire", Type = "REWARD_AFTER_PROBATION", Value = 200, MinDurationMonths = 6, Status = "PAUSED", CreatedAt = now.AddMilliseconds(-DayMs * 25) },
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
