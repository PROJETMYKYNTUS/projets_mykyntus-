using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>Recalcule les <see cref="WorkflowStepConfig.FromStatus"/> selon <see cref="WorkflowStepConfig.SortOrder"/>.</summary>
public static class WorkflowStepConfigRechain
{
    public static void ApplyToActiveSteps(IList<WorkflowStepConfig> allSteps)
    {
        var active = allSteps.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToList();
        if (active.Count == 0) return;
        active[0].FromStatus = PrimeValidationWorkflowService.Pending;
        for (var i = 1; i < active.Count; i++)
            active[i].FromStatus = active[i - 1].ToStatus;
    }
}
