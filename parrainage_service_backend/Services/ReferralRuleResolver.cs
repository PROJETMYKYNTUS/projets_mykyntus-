using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;

namespace ParrainageBackend.Services;

public sealed record SubmitPositionResolution(
    string Position,
    string PositionMode,
    string? AppliedRuleId);

public sealed record RewardDefaults(
    decimal SuggestedAmount,
    int MinDurationMonths,
    string RuleLabel);

public sealed class ReferralRuleResolver(ParrainageDbContext db)
{
    public const string PositionRuleType = "REWARD_PER_POSITION";

    public async Task<SubmitPositionResolution> ResolveOnSubmitAsync(
        string? ruleId,
        string? position,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(ruleId))
        {
            var rule = await db.ReferralRules.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == ruleId.Trim(), ct);
            if (rule == null)
                throw new InvalidOperationException("Règle de poste introuvable.");
            if (rule.Status != "ACTIVE" || rule.Type != PositionRuleType)
                throw new InvalidOperationException("La règle sélectionnée n'est pas active ou n'est pas une règle poste.");
            if (string.IsNullOrWhiteSpace(rule.Target))
                throw new InvalidOperationException("La règle poste n'a pas de cible définie.");

            return new SubmitPositionResolution(
                rule.Target.Trim(),
                ReferralPositionMode.Catalog,
                rule.Id);
        }

        if (string.IsNullOrWhiteSpace(position))
            throw new InvalidOperationException("Sélectionnez un poste catalogue ou saisissez un poste personnalisé.");

        return new SubmitPositionResolution(
            position.Trim(),
            ReferralPositionMode.Custom,
            null);
    }

    public async Task<int> ResolveMinDurationMonthsAsync(ReferralEntity referral, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(referral.AppliedRuleId))
        {
            var rule = await db.ReferralRules.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == referral.AppliedRuleId, ct);
            if (rule != null && rule.MinDurationMonths > 0)
                return rule.MinDurationMonths;
        }

        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        return cfg?.MinDurationMonths ?? DefaultSystemConfig.MinDurationMonths;
    }

    public async Task<RewardDefaults> ResolveRewardDefaultsAsync(
        ReferralEntity referral,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(referral.AppliedRuleId))
        {
            var rule = await db.ReferralRules.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == referral.AppliedRuleId, ct);
            if (rule != null)
            {
                return new RewardDefaults(
                    rule.Value,
                    rule.MinDurationMonths > 0 ? rule.MinDurationMonths : DefaultSystemConfig.MinDurationMonths,
                    $"Poste {rule.Target} ({rule.Value} DH, {rule.MinDurationMonths} mois)");
            }
        }

        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        var amount = cfg?.DefaultBonusAmount ?? DefaultSystemConfig.DefaultBonusAmount;
        var months = cfg?.MinDurationMonths ?? DefaultSystemConfig.MinDurationMonths;
        return new RewardDefaults(amount, months, $"Règle générale ({amount} DH, {months} mois)");
    }

    public async Task ValidateUniqueActiveTargetAsync(
        string? target,
        string ruleType,
        string? excludeRuleId,
        CancellationToken ct = default)
    {
        if (ruleType != PositionRuleType || string.IsNullOrWhiteSpace(target))
            return;

        var normalized = target.Trim();
        var duplicate = await db.ReferralRules.AsNoTracking()
            .AnyAsync(r =>
                r.Status == "ACTIVE" &&
                r.Type == PositionRuleType &&
                r.Target != null &&
                r.Target.Trim().ToLower() == normalized.ToLower() &&
                (excludeRuleId == null || r.Id != excludeRuleId),
                ct);

        if (duplicate)
            throw new InvalidOperationException($"Une règle active existe déjà pour le poste « {normalized} ».");
    }
}
