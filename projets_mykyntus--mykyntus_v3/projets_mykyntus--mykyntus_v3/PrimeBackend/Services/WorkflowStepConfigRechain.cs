using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>Recalcule les <see cref="WorkflowStepConfigEntity.FromStatus"/> selon <see cref="WorkflowStepConfigEntity.SortOrder"/>.</summary>
public static class WorkflowStepConfigRechain
{
    public static void ApplyToActiveSteps(IList<WorkflowStepConfigEntity> allSteps)
    {
        var active = allSteps.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToList();
        if (active.Count == 0) return;
        active[0].FromStatus = PrimeValidationWorkflowService.Pending;
        for (var i = 1; i < active.Count; i++)
            active[i].FromStatus = active[i - 1].ToStatus;
    }
}
