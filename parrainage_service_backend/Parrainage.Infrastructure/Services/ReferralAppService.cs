using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class ReferralAppService(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    ReferralEligibilityService eligibility,
    ReferralRuleResolver ruleResolver,
    ReferralCvStorageService cvStorage,
    IPlanningEmploymentCheckClient employmentCheck) : IReferralAppService
{
    public async Task<IReadOnlyList<ReferralDto>> ListAsync(CancellationToken ct = default)
    {
        await eligibility.ProcessEligibleReferralsAsync(ct);
        var rows = await db.Referrals.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ReferralHistoryDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var rows = await db.ReferralHistory.AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(h => h.ToDto()).ToList();
    }

    public async Task<ReferralDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await eligibility.ProcessEligibleReferralsAsync(ct);
        var entity = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return null;
        return await EnrichDtoAsync(entity.ToDto(), ct);
    }

    public async Task<ReferralRewardPreviewDto?> GetRewardPreviewAsync(string id, CancellationToken ct = default)
    {
        var entity = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
            return null;

        var defaults = await ruleResolver.ResolveRewardDefaultsAsync(entity, ct);
        return new ReferralRewardPreviewDto
        {
            SuggestedAmount = defaults.SuggestedAmount,
            MinDurationMonths = defaults.MinDurationMonths,
            RuleLabel = defaults.RuleLabel,
            AppliedRuleId = entity.AppliedRuleId,
            PositionMode = entity.PositionMode,
        };
    }

    public async Task<ReferralDto> CreateAsync(CreateReferralRequest body, CancellationToken ct = default)
    {
        var created = await workflow.SubmitReferralAsync(body, ct);
        return created.ToDto();
    }

    public async Task<ReferralDto?> UpdateAsync(string id, UpdateReferralRequest body, CancellationToken ct = default)
    {
        var entity = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
            return null;

        if (body.ReferrerName != null) entity.ReferrerName = body.ReferrerName;
        if (body.ProjectName != null) entity.ProjectName = body.ProjectName;
        if (body.CandidateName != null) entity.CandidateName = body.CandidateName;
        if (body.CandidateEmail != null) entity.CandidateEmail = body.CandidateEmail;
        if (body.CandidatePhone != null) entity.CandidatePhone = body.CandidatePhone;
        if (body.Position != null) entity.Position = body.Position;
        if (body.CvUrl != null) entity.CvUrl = body.CvUrl;

        if (body.Status != null)
        {
            var allowed = new[] { "SUBMITTED", "PROCESSED", "IN_TRAINING", "APPROVED", "REJECTED", "REWARDED" };
            if (!allowed.Contains(body.Status))
                throw new InvalidOperationException("status invalide (SUBMITTED|PROCESSED|IN_TRAINING|APPROVED|REJECTED|REWARDED).");
            if (body.Status == "APPROVED")
                throw new InvalidOperationException("Utilisez POST /approve pour valider un dossier.");
            entity.Status = body.Status;
        }

        if (body.RewardAmount.HasValue)
            entity.RewardAmount = body.RewardAmount.Value;

        var manualPatch = body.Status != null || body.RewardAmount.HasValue
            || body.CandidateName != null || body.CandidateEmail != null;
        if (manualPatch)
        {
            db.AuditLogs.Add(new AuditLogEntryEntity
            {
                Id = $"audit-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Action = "REFERRAL_MANUAL_UPDATE",
                UserId = body.Actor?.Id ?? "admin-1",
                UserLabel = body.Actor?.Label ?? "Administrateur",
                Timestamp = DateTimeOffset.UtcNow,
                Details = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            });
        }

        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ReferralDto?> ProcessAsync(string id, ProcessReferralRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.ProcessReferralAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> ApproveAsync(string id, ApproveReferralRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.ApproveReferralAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> ConfirmProductionAsync(string id, ConfirmProductionStartRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.ConfirmProductionStartAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> RejectEarlyDepartureAsync(string id, RejectEarlyDepartureRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.RejectEarlyDepartureAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> ExtendTrainingAsync(string id, ExtendTrainingRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.ExtendTrainingAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> ConfirmEligibilityAsync(string id, ConfirmPaymentEligibilityRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.ConfirmPaymentEligibilityAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> ChangeStatusAsync(string id, UpdateStatusRequest body, CancellationToken ct = default)
    {
        var allowed = new[] { "SUBMITTED", "REJECTED" };
        if (!allowed.Contains(body.Status))
            throw new InvalidOperationException("status invalide pour cet endpoint (SUBMITTED|REJECTED).");

        var updated = await workflow.UpdateStatusAsync(id, body.Status, body.Actor, body.Comment, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> RewardAsync(string id, RewardRequest body, ParrainageResolvedUser user, CancellationToken ct = default)
    {
        var updated = await workflow.MarkReferralPaidAsync(
            id,
            new MarkReferralPaymentRequest
            {
                Paid = true,
                PaidAt = DateTimeOffset.UtcNow,
                Actor = body.Actor ?? new ActorDto { Id = user.UserId, Label = user.Role },
            },
            ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> MarkPaymentAsync(string id, MarkReferralPaymentRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.MarkReferralPaidAsync(id, body, ct);
        return updated?.ToDto();
    }

    public async Task<ReferralDto?> UploadCvAsync(string id, IFormFile file, CancellationToken ct = default)
    {
        var entity = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
            return null;

        entity.CvUrl = await cvStorage.SaveAsync(id, file, ct);
        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ReferralCvFile?> OpenCvAsync(string id, CancellationToken ct = default)
    {
        var exists = await db.Referrals.AsNoTracking().AnyAsync(r => r.Id == id, ct);
        if (!exists)
            return null;

        var opened = cvStorage.OpenRead(id);
        if (opened is null)
            return null;

        var (stream, contentType, fileName) = opened.Value;
        return new ReferralCvFile(stream, contentType, fileName);
    }

    public async Task<IReadOnlyList<ReferralDto>> ListOnboardingAsync(CancellationToken ct = default)
    {
        var rows = await workflow.ListOnboardingReferralsAsync(ct);
        var list = new List<ReferralDto>(rows.Count);
        foreach (var row in rows)
            list.Add(await EnrichDtoAsync(row.ToDto(), ct));
        return list;
    }

    public async Task<ReferralDto?> LinkEmployeeAsync(string id, LinkEmployeeRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.LinkEmployeeAsync(id, body, ct);
        return updated is null ? null : await EnrichDtoAsync(updated.ToDto(), ct);
    }

    public async Task<ReferralDto?> CompleteOnboardingAsync(string id, CompleteOnboardingRequest body, CancellationToken ct = default)
    {
        var updated = await workflow.CompleteOnboardingAsync(id, body, ct);
        return updated is null ? null : await EnrichDtoAsync(updated.ToDto(), ct);
    }

    private async Task<ReferralDto> EnrichDtoAsync(ReferralDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.CandidateEmployeeId))
            return dto;

        var summary = await employmentCheck.GetEmploymentSummaryAsync(dto.CandidateEmployeeId, ct);
        if (summary is null)
            return dto;

        dto.EmploymentCheckSummary = new EmploymentCheckSummaryDto
        {
            IsActive = summary.IsActive,
            ContractStatus = summary.ContractStatus,
            ProbationEndDate = summary.ProbationEndDate,
            IsEligibleForPaymentConfirmation = summary.IsEligibleForPaymentConfirmation,
            BlockReason = summary.BlockReason,
        };
        return dto;
    }
}
