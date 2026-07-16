using Microsoft.EntityFrameworkCore;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Domain.Entities;

namespace Parrainage.Infrastructure.Services;

/// <summary>
/// Seed parrainage ancré sur le roster contact centre (e1–e11).
/// ReferrerId = Auth SubjectId pour « mes parrainages » via JWT.
/// </summary>
public static class ParrainageSeeder
{
    private const long HalfDayMs = 1000L * 60 * 60 * 12;
    private const long DayMs = 86_400_000L;
    private const int SeedVersion = 2;

    public static async Task SeedAsync(ParrainageDbContext db, ILogger logger, CancellationToken ct)
    {
        var hasLegacyDemo = await db.Referrals.AnyAsync(
            r => r.ReferrerName.Contains("Démo") || r.ReferrerId.StartsWith("kyntus-"), ct);

        if (await db.Referrals.AnyAsync(ct) && !hasLegacyDemo)
        {
            logger.LogInformation("PARRAINAGE : données contact centre déjà présentes — seed ignoré.");
            await EnsureSingletonsAsync(db, ct);
            await SeedPilotageReferralsAsync(db, logger, ct);
            return;
        }

        if (hasLegacyDemo)
        {
            logger.LogInformation("PARRAINAGE : remplacement du seed démo (v{Version}) par le roster contact centre.", SeedVersion);
            db.ReferralNotifications.RemoveRange(await db.ReferralNotifications.ToListAsync(ct));
            db.ReferralHistory.RemoveRange(await db.ReferralHistory.ToListAsync(ct));
            db.Referrals.RemoveRange(await db.Referrals.ToListAsync(ct));
            if (!await db.ReferralRules.AnyAsync(ct))
                db.ReferralRules.AddRange(BuildRules());
            await db.SaveChangesAsync(ct);
        }
        else
        {
            logger.LogInformation("PARRAINAGE : seed contact centre v{Version} (15 parrainages + 3 règles).", SeedVersion);
            db.ReferralRules.AddRange(BuildRules());
        }

        var referrals = BuildReferrals();
        db.Referrals.AddRange(referrals);

        var (history, notifications) = BuildHistoryAndNotifications(referrals);
        db.ReferralHistory.AddRange(history);
        db.ReferralNotifications.AddRange(notifications);

        await EnsureSingletonsAsync(db, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : seed contact centre terminé.");

        await SeedPilotageReferralsAsync(db, logger, ct);
    }

    /// <summary>Ajoute des parrainages du pôle pilotage performance (idempotent via ref-pilot-*).</summary>
    public static async Task SeedPilotageReferralsAsync(ParrainageDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Referrals.AnyAsync(r => r.Id.StartsWith("ref-pilot-"), ct))
        {
            logger.LogInformation("PARRAINAGE : parrainages pilotage déjà présents.");
            return;
        }

        var chaima = PilotagePerformanceRoster.Require("Benali");
        var hamid = PilotagePerformanceRoster.Require("Fellah");
        var othmane = PilotagePerformanceRoster.Require("Kabbaj");
        var younes = PilotagePerformanceRoster.Require("Elidrissi");
        var salim = PilotagePerformanceRoster.Require("Ouazzani");
        var malak = PilotagePerformanceRoster.Require("Souiri");
        var asmae = PilotagePerformanceRoster.Require("Tazi");

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = new (string Id, PilotagePerformanceRoster.Employee Referrer, string CandidateName, string CandidateEmail, string CandidatePhone, string Position, string Status)[]
        {
            ("ref-pilot-01", chaima, "Imane Sebbar", "imane.sebbar@candidat.ma", "+212 6 20 30 40 50", "Agent 1er niveau", "SUBMITTED"),
            ("ref-pilot-02", hamid, "Karim Benchekroun", "karim.benchekroun@candidat.ma", "+212 6 21 31 41 51", "Conseiller client", "PROCESSED"),
            ("ref-pilot-03", othmane, "Sara Filali", "sara.filali@candidat.ma", "+212 6 22 32 42 52", "Analyste données", "APPROVED"),
            ("ref-pilot-04", younes, "Mehdi Amrani", "mehdi.amrani@candidat.ma", "+212 6 23 33 43 53", "Référent technique", "IN_TRAINING"),
            ("ref-pilot-05", salim, "Nada Cherkaoui", "nada.cherkaoui@candidat.ma", "+212 6 24 34 44 54", "Superviseur cellule", "SUBMITTED"),
            ("ref-pilot-06", malak, "Yassine Lahbabi", "yassine.lahbabi@candidat.ma", "+212 6 25 35 45 55", "Chef de projet", "REWARDED"),
            ("ref-pilot-07", asmae, "Leila Mansour", "leila.mansour@candidat.ma", "+212 6 26 36 46 56", "Agent chat", "REJECTED"),
            ("ref-pilot-08", chaima, "Omar Belhaj", "omar.belhaj@candidat.ma", "+212 6 27 37 47 57", "Agent 1er niveau", "IN_TRAINING"),
        };

        var referrals = new List<ReferralEntity>();
        for (var idx = 0; idx < seed.Length; idx++)
        {
            var r = seed[idx];
            var rewardAmount = r.Status switch
            {
                "REWARDED" => 700m,
                "IN_TRAINING" => 750m,
                _ => 0m,
            };
            var createdAt = now.AddMilliseconds(-(idx * (double)HalfDayMs + idx * (double)DayMs));
            var entity = new ReferralEntity
            {
                Id = r.Id,
                ReferrerId = PilotagePerformanceRoster.ReferrerId(r.Referrer),
                ReferrerName = PilotagePerformanceRoster.DisplayName(r.Referrer),
                ProjectId = "proj-pilotage",
                ProjectName = "Pilotage performance — analyse opérationnelle",
                TeamId = "team-kpi",
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
                entity.TrainingEndDate = today.AddDays(10);
            }

            referrals.Add(entity);
        }

        db.Referrals.AddRange(referrals);
        var (history, notifications) = BuildHistoryAndNotifications(referrals);
        db.ReferralHistory.AddRange(history);
        db.ReferralNotifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : {Count} parrainages pilotage performance ajoutés.", referrals.Count);
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
        var e1 = ContactCentreRoster.Require("e1");
        var e3 = ContactCentreRoster.Require("e3");
        var e8 = ContactCentreRoster.Require("e8");
        var e9 = ContactCentreRoster.Require("e9");
        var e10 = ContactCentreRoster.Require("e10");

        var now = DateTimeOffset.UtcNow;
        // Candidats externes — pas de collision avec e1–e11
        var seed = new (string Id, ContactCentreRoster.Employee Referrer, string ProjectId, string ProjectName, string TeamId, string CandidateName, string CandidateEmail, string CandidatePhone, string Position, string Status)[]
        {
            ("ref-1001", e1, "proj-inbound", "Inbound grands comptes", "team-voice", "Fatima Zahra Bennis", "fatima.bennis@candidat.ma", "+212 6 12 34 56 78", "Agent 1er niveau", "SUBMITTED"),
            ("ref-1002", e1, "proj-inbound", "Inbound grands comptes", "team-voice", "Amine El Fassi", "amine.elfassi@candidat.ma", "+212 6 98 76 54 32", "Conseiller client", "PROCESSED"),
            ("ref-1003", e8, "proj-retention", "Réclamations & rétention", "team-ret", "Salma Idrissi", "salma.idrissi@candidat.ma", "+212 6 11 22 33 44", "Agent rétention", "REJECTED"),
            ("ref-1004", e8, "proj-retention", "Réclamations & rétention", "team-ret", "Youssef Alaoui", "youssef.alaoui@candidat.ma", "+212 6 55 66 77 88", "Conseiller client", "REWARDED"),
            ("ref-1005", e3, "proj-acd", "Supervision connectivité & ACD", "team-si", "Khadija Benjelloun", "khadija.benjelloun@candidat.ma", "+212 6 44 55 66 77", "Technicien réseau", "SUBMITTED"),
            ("ref-1006", e1, "proj-inbound", "Inbound grands comptes", "team-chat", "Hassan Tazi", "hassan.tazi@candidat.ma", "+212 6 22 33 44 55", "Agent chat", "APPROVED"),
            ("ref-1007", e9, "proj-retention", "Réclamations & rétention", "team-ret", "Sara Ouazzani", "sara.ouazzani@candidat.ma", "+212 6 77 88 99 00", "Superviseur cellule", "SUBMITTED"),
            ("ref-1008", e1, "proj-acd", "Supervision connectivité & ACD", "team-si", "Reda Bennani", "reda.bennani@candidat.ma", "+212 6 10 20 30 40", "Superviseur ACD", "APPROVED"),
            ("ref-1009", e3, "proj-inbound", "Inbound grands comptes", "team-voice", "Amina Chafik", "amina.chafik@candidat.ma", "+212 6 31 41 51 61", "Coach qualité", "REJECTED"),
            ("ref-1010", e8, "proj-retention", "Réclamations & rétention", "team-ret", "Younes Kadiri", "younes.kadiri@candidat.ma", "+212 6 52 62 72 82", "Agent 1er niveau", "REWARDED"),
            ("ref-1011", e9, "proj-acd", "Supervision connectivité & ACD", "team-si", "Noura Sebti", "noura.sebti@candidat.ma", "+212 6 93 83 73 63", "Chef de projet", "SUBMITTED"),
            ("ref-1012", e1, "proj-inbound", "Inbound grands comptes", "team-voice", "Samir Belkadi", "samir.belkadi@candidat.ma", "+212 6 12 34 56 79", "Agent 1er niveau", "SUBMITTED"),
            ("ref-1013", e10, "proj-retention", "Réclamations & rétention", "team-ret", "Ibtissam Rami", "ibtissam.rami@candidat.ma", "+212 6 14 24 34 44", "Référent technique", "APPROVED"),
            ("ref-1014", e3, "proj-acd", "Supervision connectivité & ACD", "team-si", "Adil Mernissi", "adil.mernissi@candidat.ma", "+212 6 15 25 35 45", "Analyste données", "IN_TRAINING"),
            ("ref-1015", e8, "proj-inbound", "Inbound grands comptes", "team-chat", "Zineb Harakat", "zineb.harakat@candidat.ma", "+212 6 16 26 36 46", "Agent chat", "IN_TRAINING"),
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
                ReferrerId = ContactCentreRoster.ReferrerId(r.Referrer),
                ReferrerName = ContactCentreRoster.DisplayName(r.Referrer),
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
        var rh = ContactCentreRoster.Require("e5");
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
                    PerformedById = ContactCentreRoster.ReferrerId(rh),
                    PerformedByLabel = ContactCentreRoster.DisplayName(rh),
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
                    PerformedById = ContactCentreRoster.ReferrerId(rh),
                    PerformedByLabel = ContactCentreRoster.DisplayName(rh),
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
                    PerformedById = ContactCentreRoster.ReferrerId(rh),
                    PerformedByLabel = ContactCentreRoster.DisplayName(rh),
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
                    PerformedById = ContactCentreRoster.ReferrerId(rh),
                    PerformedByLabel = ContactCentreRoster.DisplayName(rh),
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
                    PerformedById = ContactCentreRoster.ReferrerId(rh),
                    PerformedByLabel = ContactCentreRoster.DisplayName(rh),
                    CreatedAt = submittedAt.AddMilliseconds(DayMs * 16),
                    RewardAmount = r.RewardAmount,
                });
            }
        }

        return (history, notifications);
    }
}
