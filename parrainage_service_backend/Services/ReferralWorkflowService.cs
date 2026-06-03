using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Models;

namespace ParrainageBackend.Services;

/// <summary>
/// Workflow parrainage : soumission, validation RH (sans paiement), éligibilité, marquage payé compta.
/// </summary>
public sealed class ReferralWorkflowService(ParrainageDbContext db, ReferralRuleResolver ruleResolver, ReferralCvStorageService cvStorage)
{
    private static readonly Dictionary<string, string> StatusLabelFr = new()
    {
        ["SUBMITTED"] = "En attente",
        ["PROCESSED"] = "Dossier traité",
        ["APPROVED"] = "Validé",
        ["REJECTED"] = "Rejeté",
        ["REWARDED"] = "Prime versée",
    };

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<ReferralEntity> SubmitReferralAsync(CreateReferralRequest data, CancellationToken ct)
    {
        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        var limit = cfg?.ReferralLimitPerEmployee ?? 10;
        var existingCount = await db.Referrals.CountAsync(r => r.ReferrerId == data.ReferrerId, ct);
        if (existingCount >= limit)
            throw new InvalidOperationException(
                $"Limite de parrainages atteinte pour cet employé ({limit} max).");

        var id = $"ref-{NowMs()}";
        var createdAt = DateTimeOffset.UtcNow;
        var positionResolution = await ruleResolver.ResolveOnSubmitAsync(data.RuleId, data.Position, ct);
        var created = new ReferralEntity
        {
            Id = id,
            ReferrerId = data.ReferrerId,
            ReferrerName = data.ReferrerName,
            ProjectId = "proj-1",
            ProjectName = string.IsNullOrWhiteSpace(data.Project) ? "Projet" : data.Project!,
            TeamId = "team-a",
            CandidateName = data.CandidateName,
            CandidateEmail = data.CandidateEmail,
            CandidatePhone = data.CandidatePhone,
            Position = positionResolution.Position,
            PositionMode = positionResolution.PositionMode,
            AppliedRuleId = positionResolution.AppliedRuleId,
            Status = "SUBMITTED",
            RewardAmount = 0,
            PaymentStatus = ReferralPaymentStatus.NotEligible,
            Notes = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim(),
            CreatedAt = createdAt,
        };
        db.Referrals.Add(created);

        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{id}-sub",
            ReferralId = id,
            CandidateName = data.CandidateName,
            Action = "SUBMITTED",
            PerformedById = data.ReferrerId,
            PerformedByLabel = data.ReferrerName,
            CreatedAt = createdAt,
            Comment = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim(),
        });

        db.ReferralNotifications.Add(new ReferralNotificationEntity
        {
            Id = $"nt-{id}-sub",
            Type = "NEW_REFERRAL",
            Message = $"Nouveau parrainage : {data.CandidateName} ({positionResolution.Position})",
            CreatedAt = createdAt,
            Read = false,
            ReferralId = id,
            ReferrerId = data.ReferrerId,
            TargetRoles = new() { "RH", "ADMIN", "MANAGER", "COACH", "RP" },
        });

        await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>RH marque la candidature comme traitée (avant l'entrée effective du candidat).</summary>
    public async Task<ReferralEntity?> ProcessReferralAsync(
        string id,
        ProcessReferralRequest request,
        CancellationToken ct)
    {
        var current = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (current == null) return null;
        if (current.Status != "SUBMITTED")
            throw new InvalidOperationException("Seuls les dossiers en attente peuvent être marqués comme traités.");
        if (string.IsNullOrWhiteSpace(current.CvUrl) && !cvStorage.Exists(current.Id))
            throw new InvalidOperationException("Un CV candidat est obligatoire avant le traitement RH.");

        var now = DateTimeOffset.UtcNow;
        current.Status = "PROCESSED";

        var actor = ResolveActor(request.Actor, "rh-1", "RH");
        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{id}-{NowMs()}",
            ReferralId = id,
            CandidateName = current.CandidateName,
            Action = "PROCESSED",
            PerformedById = actor.Id,
            PerformedByLabel = actor.Label,
            CreatedAt = now,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
        });

        AddStatusNotification(current, "PROCESSED", now);
        await db.SaveChangesAsync(ct);
        return current;
    }

    public async Task<ReferralEntity?> ApproveReferralAsync(
        string id,
        ApproveReferralRequest request,
        CancellationToken ct)
    {
        var current = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (current == null) return null;
        if (current.Status != "PROCESSED")
            throw new InvalidOperationException("Seuls les dossiers traités par la RH peuvent être validés (entrée candidat).");

        if (request.RewardAmount <= 0)
            throw new InvalidOperationException("Le montant engagé doit être supérieur à 0.");

        var minMonths = await ruleResolver.ResolveMinDurationMonthsAsync(current, ct);

        var now = DateTimeOffset.UtcNow;
        var eligibleAt = ReferralEligibilityCalculator.ComputeEligibleForPayment(
            request.CandidateStartDate,
            minMonths);

        current.Status = "APPROVED";
        current.RewardAmount = request.RewardAmount;
        current.CandidateStartDate = request.CandidateStartDate;
        current.ApprovedAt = now;
        current.EligibleForPaymentAt = eligibleAt;
        current.PaymentStatus = ReferralPaymentStatus.NotEligible;
        current.EligibilityNotifiedAt = null;

        var actor = ResolveActor(request.Actor, "rh-1", "RH");
        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{id}-{NowMs()}",
            ReferralId = id,
            CandidateName = current.CandidateName,
            Action = "APPROVED",
            PerformedById = actor.Id,
            PerformedByLabel = actor.Label,
            CreatedAt = now,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            RewardAmount = request.RewardAmount,
        });

        AddStatusNotification(current, "APPROVED", now);

        await db.SaveChangesAsync(ct);
        return current;
    }

    /// <summary>RH confirme que le candidat est toujours en poste — transmission à la comptabilité.</summary>
    public async Task<ReferralEntity?> ConfirmPaymentEligibilityAsync(
        string id,
        ConfirmPaymentEligibilityRequest request,
        CancellationToken ct)
    {
        var current = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (current == null) return null;
        if (current.Status != "APPROVED")
            throw new InvalidOperationException("Seuls les dossiers validés peuvent être confirmés pour le paiement.");
        if (current.PaymentStatus != ReferralPaymentStatus.AwaitingRh)
            throw new InvalidOperationException(
                "Ce dossier n'est pas en attente de confirmation RH (période minimum non atteinte ou déjà transmis).");

        var now = DateTimeOffset.UtcNow;
        var actor = ResolveActor(request.Actor, "rh-1", "RH");
        MarkPaymentReady(current, now, actor);

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            db.ReferralHistory.Add(new ReferralHistoryEntryEntity
            {
                Id = $"hist-{id}-elig-{NowMs()}",
                ReferralId = id,
                CandidateName = current.CandidateName,
                Action = "ELIGIBILITY_CONFIRMED",
                PerformedById = actor.Id,
                PerformedByLabel = actor.Label,
                CreatedAt = now,
                Comment = request.Comment.Trim(),
                RewardAmount = current.RewardAmount,
            });
        }

        await db.SaveChangesAsync(ct);
        return current;
    }

    public async Task<ReferralEntity?> UpdateStatusAsync(
        string id,
        string status,
        ActorDto? actor,
        string? comment,
        CancellationToken ct)
    {
        var current = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (current == null) return null;
        if (current.Status == "REWARDED") return current;

        if (status == "APPROVED")
            throw new InvalidOperationException("Utilisez l'endpoint approve avec date d'entrée et montant engagé.");

        current.Status = status;
        if (status != "REWARDED")
        {
            current.RewardAmount = 0;
            current.PaymentStatus = ReferralPaymentStatus.NotEligible;
            current.CandidateStartDate = null;
            current.ApprovedAt = null;
            current.EligibleForPaymentAt = null;
            current.EligibilityNotifiedAt = null;
        }

        var now = DateTimeOffset.UtcNow;
        var resolved = ResolveActor(actor, "rh-1", "RH");
        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{id}-{NowMs()}",
            ReferralId = id,
            CandidateName = current.CandidateName,
            Action = status,
            PerformedById = resolved.Id,
            PerformedByLabel = resolved.Label,
            CreatedAt = now,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
        });

        AddStatusNotification(current, status, now);
        await db.SaveChangesAsync(ct);
        return current;
    }

    /// <summary>Legacy endpoint — délègue au marquage compta si éligible.</summary>
    public Task<ReferralEntity?> AssignRewardAsync(string id, decimal amount, ActorDto? actor, CancellationToken ct) =>
        MarkReferralPaidAsync(
            id,
            new MarkReferralPaymentRequest
            {
                Paid = true,
                PaidAt = DateTimeOffset.UtcNow,
                Actor = actor,
            },
            ct);

    public async Task<ReferralEntity?> MarkReferralPaidAsync(
        string id,
        MarkReferralPaymentRequest request,
        CancellationToken ct)
    {
        var current = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (current == null) return null;

        var actor = ResolveActor(request.Actor, "compta-1", "Comptabilité");
        var now = DateTimeOffset.UtcNow;

        if (request.Paid)
        {
            if (current.Status != "APPROVED" || current.PaymentStatus != ReferralPaymentStatus.Ready)
                throw new InvalidOperationException(
                    "Seuls les dossiers approuvés et éligibles au paiement peuvent être marqués payés.");

            var paidAt = request.PaidAt ?? now;
            current.Status = "REWARDED";
            current.PaymentStatus = ReferralPaymentStatus.Paid;
            current.PaidAt = paidAt;
            current.PaidByUserId = actor.Id;
            current.PaidByLabel = actor.Label;
            current.PaymentReference = string.IsNullOrWhiteSpace(request.Reference)
                ? null
                : request.Reference.Trim();

            db.ReferralHistory.Add(new ReferralHistoryEntryEntity
            {
                Id = $"hist-{id}-rew-{NowMs()}",
                ReferralId = id,
                CandidateName = current.CandidateName,
                Action = "REWARDED",
                PerformedById = actor.Id,
                PerformedByLabel = actor.Label,
                CreatedAt = paidAt,
                RewardAmount = current.RewardAmount,
                Comment = current.PaymentReference,
            });

            db.ReferralNotifications.Add(new ReferralNotificationEntity
            {
                Id = $"nt-{id}-rew-{NowMs()}",
                Type = "REFERRAL_REWARDED",
                Message = $"Prime versée : {current.CandidateName} ({current.RewardAmount} DH)",
                CreatedAt = paidAt,
                Read = false,
                ReferralId = id,
                ReferrerId = current.ReferrerId,
                TargetRoles = new() { "RH", "ADMIN", "PILOTE", "COMPTA", "COMPTABILITE" },
            });
        }
        else
        {
            if (current.Status != "REWARDED" || current.PaymentStatus != ReferralPaymentStatus.Paid)
                throw new InvalidOperationException("Seuls les dossiers payés peuvent être annulés.");

            current.Status = "APPROVED";
            current.PaymentStatus = ReferralPaymentStatus.Ready;
            current.PaidAt = null;
            current.PaidByUserId = null;
            current.PaidByLabel = null;
            current.PaymentReference = null;

            db.ReferralHistory.Add(new ReferralHistoryEntryEntity
            {
                Id = $"hist-{id}-unpay-{NowMs()}",
                ReferralId = id,
                CandidateName = current.CandidateName,
                Action = "PAYMENT_UNDONE",
                PerformedById = actor.Id,
                PerformedByLabel = actor.Label,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return current;
    }

    internal void MarkAwaitingRhConfirmation(ReferralEntity current, DateTimeOffset now)
    {
        if (current.PaymentStatus != ReferralPaymentStatus.NotEligible) return;

        current.PaymentStatus = ReferralPaymentStatus.AwaitingRh;

        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{current.Id}-due-{NowMs()}",
            ReferralId = current.Id,
            CandidateName = current.CandidateName,
            Action = "ELIGIBILITY_DUE",
            PerformedById = "system",
            PerformedByLabel = "Système",
            CreatedAt = now,
            RewardAmount = current.RewardAmount,
        });

        db.ReferralNotifications.Add(new ReferralNotificationEntity
        {
            Id = $"nt-{current.Id}-due-{NowMs()}",
            Type = "REFERRAL_ELIGIBILITY_DUE",
            Message =
                $"Éligibilité à confirmer : {current.CandidateName} — vérifiez que le candidat est toujours en poste avant transmission compta ({current.RewardAmount} DH).",
            CreatedAt = now,
            Read = false,
            ReferralId = current.Id,
            ReferrerId = current.ReferrerId,
            TargetRoles = new() { "RH", "ADMIN" },
        });
    }

    internal void MarkPaymentReady(
        ReferralEntity current,
        DateTimeOffset now,
        (string Id, string Label)? actor = null)
    {
        if (current.PaymentStatus == ReferralPaymentStatus.Paid) return;
        if (current.PaymentStatus == ReferralPaymentStatus.Ready && current.EligibilityNotifiedAt.HasValue)
            return;

        current.PaymentStatus = ReferralPaymentStatus.Ready;
        current.EligibilityNotifiedAt = now;

        var performedBy = actor ?? ("system", "Système");

        db.ReferralHistory.Add(new ReferralHistoryEntryEntity
        {
            Id = $"hist-{current.Id}-ready-{NowMs()}",
            ReferralId = current.Id,
            CandidateName = current.CandidateName,
            Action = "PAYMENT_READY",
            PerformedById = performedBy.Id,
            PerformedByLabel = performedBy.Label,
            CreatedAt = now,
            RewardAmount = current.RewardAmount,
        });

        db.ReferralNotifications.Add(new ReferralNotificationEntity
        {
            Id = $"nt-{current.Id}-ready-{NowMs()}",
            Type = "REFERRAL_PAYMENT_READY",
            Message =
                $"Parrainage éligible au versement : {current.CandidateName} — {current.RewardAmount} DH (parrain : {current.ReferrerName}).",
            CreatedAt = now,
            Read = false,
            ReferralId = current.Id,
            ReferrerId = current.ReferrerId,
            TargetRoles = new() { "PILOTE", "RH", "COMPTA", "COMPTABILITE", "ADMIN" },
        });
    }

    private void AddStatusNotification(ReferralEntity current, string status, DateTimeOffset now)
    {
        var label = StatusLabelFr.TryGetValue(status, out var l) ? l : status;
        db.ReferralNotifications.Add(new ReferralNotificationEntity
        {
            Id = $"nt-{current.Id}-{NowMs()}",
            Type = "STATUS_CHANGED",
            Message = $"Statut : {label} — {current.CandidateName}",
            CreatedAt = now,
            Read = false,
            ReferralId = current.Id,
            ReferrerId = current.ReferrerId,
            TargetRoles = new() { "ALL" },
        });
    }

    private static (string Id, string Label) ResolveActor(ActorDto? actor, string defaultId, string defaultLabel) =>
        (
            string.IsNullOrWhiteSpace(actor?.Id) ? defaultId : actor!.Id!,
            string.IsNullOrWhiteSpace(actor?.Label) ? defaultLabel : actor!.Label!
        );

    /// <summary>Mirror of getNotificationsForRole in referral.service.ts.</summary>
    public List<ReferralNotificationEntity> FilterNotificationsForRole(
        List<ReferralNotificationEntity> all,
        List<ReferralEntity> referrals,
        string? role,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(role)) return all;

        return all.Where(n =>
        {
            var targets = n.TargetRoles;
            if (targets.Count > 0 && !targets.Contains("ALL"))
            {
                if (!targets.Contains(role!) &&
                    !(role is "COMPTABILITE" && targets.Contains("COMPTA")) &&
                    !(role is "COMPTA" && targets.Contains("COMPTABILITE")))
                    return false;
            }

            if (role == "PILOTE")
            {
                if (!string.IsNullOrEmpty(n.ReferrerId) && n.ReferrerId != userId) return false;
                if (!string.IsNullOrEmpty(n.ReferralId))
                {
                    var refEntity = referrals.FirstOrDefault(r => r.Id == n.ReferralId);
                    if (refEntity != null && refEntity.ReferrerId != userId) return false;
                }
            }

            if ((role == "MANAGER" || role == "COACH") && !string.IsNullOrEmpty(n.ReferralId))
            {
                var refEntity = referrals.FirstOrDefault(r => r.Id == n.ReferralId);
                if (refEntity != null && !OrgHierarchy.IsReferrerUnderManager(userId ?? string.Empty, refEntity.ReferrerId))
                    return false;
            }

            return true;
        }).ToList();
    }
}
