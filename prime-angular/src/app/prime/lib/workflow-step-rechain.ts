import type { WorkflowStepConfigDto } from '../services/prime-admin.service';

/** Statut initial des fiches (aligné sur PrimeValidationWorkflowService.Pending). */
export const WORKFLOW_ENTRY_STATUS = 'Pending';

/**
 * Recalcule les FromStatus des étapes actives selon SortOrder :
 * Pending → toStatus₁ → toStatus₂ → …
 * Les ToStatus / rôles restent attachés à chaque ligne (réordonner = changer qui valide quand).
 */
export function rechainWorkflowSteps(steps: WorkflowStepConfigDto[]): WorkflowStepConfigDto[] {
  const active = [...steps]
    .filter((s) => s.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder);
  if (active.length === 0) return steps;

  const fromById = new Map<string, string>();
  fromById.set(active[0]!.id, WORKFLOW_ENTRY_STATUS);
  for (let i = 1; i < active.length; i++) {
    fromById.set(active[i]!.id, active[i - 1]!.toStatus);
  }

  return steps.map((s) => {
    const fromStatus = fromById.get(s.id);
    return fromStatus !== undefined ? { ...s, fromStatus } : s;
  });
}

type PipelineStep = Pick<WorkflowStepConfigDto, 'sortOrder' | 'approverRole' | 'isActive'>;

const FICHE_OPERATIONAL_ROLES = new Set([
  'Référent technique',
  'Superviseur',
  'Chef de projet',
]);

/** Libellé « Rôle (niveau n) » pour l’UI fiches (Référent → Superviseur → Chef). */
export function formatWorkflowPipeline(steps: PipelineStep[] | undefined): string {
  const active = [...(steps ?? [])]
    .filter((s) => s.isActive && FICHE_OPERATIONAL_ROLES.has(s.approverRole))
    .sort((a, b) => a.sortOrder - b.sortOrder);
  if (active.length === 0) {
    return 'Aucune étape active en base — configurez le workflow dans Paramètres.';
  }
  return active.map((s, i) => `${s.approverRole} (niveau ${i + 1})`).join(' → ');
}
