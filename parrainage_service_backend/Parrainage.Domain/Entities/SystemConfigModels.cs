namespace Parrainage.Domain.Entities;

// Plain C# mirrors of the TypeScript system-config model. These are stored as JSON
// columns on the SystemConfig row and returned verbatim in the config DTO so the
// JSON shapes match the Angular interfaces.

public sealed class ReferralBonusTier
{
    public string Id { get; set; } = string.Empty;
    public decimal AmountDH { get; set; }
    public int AfterMonths { get; set; }
}

public sealed class ReferralProgramRules
{
    public string ActiveMode { get; set; } = "STANDARD";
    public List<ReferralBonusTier> StandardTiers { get; set; } = new();
    public List<ReferralBonusTier> CriticalPeriodTiers { get; set; } = new();
}

public sealed class WorkflowStepConfig
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int SlaHours { get; set; }
    public List<string> Actions { get; set; } = new();
    public string NotificationType { get; set; } = "email";
    public bool NotificationEnabled { get; set; }
}

public sealed class AuditAccessConfig
{
    public bool Enabled { get; set; }
    public bool ReadOnly { get; set; }
    public bool Logs { get; set; }
    public bool History { get; set; }
    public bool Export { get; set; }
}

public sealed class AdminWorkflowConfig
{
    public List<WorkflowStepConfig> Steps { get; set; } = new();
    public AuditAccessConfig AuditAccess { get; set; } = new();
}

public static class DefaultSystemConfig
{
    public const int DefaultBonusAmount = 1500;
    public const int MinDurationMonths = 6;
    public const int ReferralLimitPerEmployee = 10;
    public const int PendingReferralAlertThreshold = 5;

    public static ReferralProgramRules ProgramRules() => new()
    {
        ActiveMode = "STANDARD",
        StandardTiers = new()
        {
            new ReferralBonusTier { Id = "tier-std-1", AmountDH = 1500, AfterMonths = 6 },
        },
        CriticalPeriodTiers = new()
        {
            new ReferralBonusTier { Id = "tier-crit-1", AmountDH = 500, AfterMonths = 3 },
            new ReferralBonusTier { Id = "tier-crit-2", AmountDH = 1000, AfterMonths = 6 },
        },
    };

    public static AdminWorkflowConfig Workflow() => new()
    {
        Steps = new()
        {
            new WorkflowStepConfig { Id = "wf-coach", Role = "Coach", SlaHours = 24, Actions = new() { "Validate", "Reject" }, NotificationType = "email", NotificationEnabled = true },
            new WorkflowStepConfig { Id = "wf-manager", Role = "Manager", SlaHours = 24, Actions = new() { "Validate", "Reject", "Approve" }, NotificationType = "email", NotificationEnabled = true },
            new WorkflowStepConfig { Id = "wf-rp", Role = "RP", SlaHours = 24, Actions = new() { "Approve", "Reject" }, NotificationType = "in-app", NotificationEnabled = true },
            new WorkflowStepConfig { Id = "wf-rh", Role = "RH", SlaHours = 48, Actions = new() { "Approve", "Reject", "Archive" }, NotificationType = "email", NotificationEnabled = true },
        },
        AuditAccess = new AuditAccessConfig { Enabled = true, ReadOnly = true, Logs = true, History = true, Export = true },
    };
}
