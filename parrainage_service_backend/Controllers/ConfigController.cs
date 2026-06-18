using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Models;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/config")]
public sealed class ConfigController(ParrainageDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemConfigDto>> Get(CancellationToken ct)
    {
        var cfg = await LoadConfigAsync(ct);
        return Ok(cfg.ToDto());
    }

    [HttpPatch]
    public async Task<ActionResult<SystemConfigDto>> Update([FromBody] UpdateConfigRequest body, CancellationToken ct)
    {
        var cfg = await LoadConfigAsync(ct);

        if (body.DefaultBonusAmount.HasValue) cfg.DefaultBonusAmount = body.DefaultBonusAmount.Value;
        if (body.MinDurationMonths.HasValue) cfg.MinDurationMonths = body.MinDurationMonths.Value;
        if (body.ReferralLimitPerEmployee.HasValue) cfg.ReferralLimitPerEmployee = body.ReferralLimitPerEmployee.Value;
        if (body.PendingReferralAlertThreshold.HasValue) cfg.PendingReferralAlertThreshold = body.PendingReferralAlertThreshold.Value;
        if (body.ReferralProgramRules != null) cfg.ReferralProgramRules = body.ReferralProgramRules;

        // Mirror admin.service.ts: an RH actor cannot mutate the workflow configuration.
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

        return Ok(cfg.ToDto());
    }

    private async Task<SystemConfigEntity> LoadConfigAsync(CancellationToken ct)
    {
        var cfg = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (cfg == null)
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
