using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Domain.Entities;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class SystemConfigAppService(ParrainageDbContext db) : ISystemConfigAppService
{
    public async Task<SystemConfigDto> GetAsync(CancellationToken ct = default)
    {
        var cfg = await LoadConfigAsync(ct);
        return cfg.ToDto();
    }

    public async Task<SystemConfigDto> UpdateAsync(UpdateConfigRequest body, CancellationToken ct = default)
    {
        var cfg = await LoadConfigAsync(ct);

        if (body.DefaultBonusAmount.HasValue) cfg.DefaultBonusAmount = body.DefaultBonusAmount.Value;
        if (body.MinDurationMonths.HasValue) cfg.MinDurationMonths = body.MinDurationMonths.Value;
        if (body.ReferralLimitPerEmployee.HasValue) cfg.ReferralLimitPerEmployee = body.ReferralLimitPerEmployee.Value;
        if (body.PendingReferralAlertThreshold.HasValue) cfg.PendingReferralAlertThreshold = body.PendingReferralAlertThreshold.Value;
        if (body.ReferralProgramRules != null) cfg.ReferralProgramRules = body.ReferralProgramRules;

        var isRh = string.Equals(body.Actor?.Role, "RH", StringComparison.OrdinalIgnoreCase);
        if (body.AdminWorkflow != null && !isRh) cfg.AdminWorkflow = body.AdminWorkflow;

        await db.SaveChangesAsync(ct);

        db.AuditLogs.Add(new AuditLogEntryEntity
        {
            Id = $"audit-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Action = "CONFIG_UPDATE",
            UserId = body.Actor?.Id ?? "system",
            UserLabel = body.Actor?.Label ?? "Système",
            Timestamp = DateTimeOffset.UtcNow,
            Details = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
        });
        await db.SaveChangesAsync(ct);

        return cfg.ToDto();
    }

    private async Task<SystemConfigEntity> LoadConfigAsync(CancellationToken ct)
    {
        var cfg = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (cfg is null)
        {
            cfg = new SystemConfigEntity
            {
                Id = 1,
                DefaultBonusAmount = DefaultSystemConfig.DefaultBonusAmount,
                MinDurationMonths = DefaultSystemConfig.MinDurationMonths,
                ReferralLimitPerEmployee = DefaultSystemConfig.ReferralLimitPerEmployee,
                PendingReferralAlertThreshold = DefaultSystemConfig.PendingReferralAlertThreshold,
                ReferralProgramRules = DefaultSystemConfig.ProgramRules(),
                AdminWorkflow = DefaultSystemConfig.Workflow(),
            };
            db.SystemConfigs.Add(cfg);
            await db.SaveChangesAsync(ct);
        }

        return cfg;
    }
}
