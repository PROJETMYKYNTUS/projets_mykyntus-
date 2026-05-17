import type { Role } from '../models';

/** Pilote : fiche fusionnée visible seulement après toutes les validations (diffusion complète). */
export function roleMustWaitForPrimeDistribution(role: string): boolean {
  return role.trim() === 'Pilote';
}

/** Rôles opérationnels + transverses de validation (hors pilote / compta). */
export function roleMayPreviewMergedFicheWithoutDistributionLock(role: Role | string): boolean {
  return !roleMustWaitForPrimeDistribution(role);
}

export interface MergedFicheActionContext {
  fillingStatus: string;
  cellulePrimeDraftId: string | null;
  polePrimeDraftId?: string | null;
  linkedTemplateId?: string | null;
  poolDistributionUnlocked?: boolean;
}

/**
 * Aperçu / export fusionné (pilotage superviseur) : données prêtes ;
 * seul le Pilote attend la diffusion complète sur la fiche fusionnée.
 */
export function mergedFicheActionsEnabled(
  role: Role | string,
  emp: MergedFicheActionContext,
  cell: { linkedTemplateId?: string | null; poolDistributionUnlocked?: boolean },
  hasDraftId: boolean,
): boolean {
  if (emp.fillingStatus.trim().toLowerCase() !== 'complete') return false;
  if (!hasDraftId) return false;
  if (!(cell.linkedTemplateId ?? '').trim()) return false;
  if (roleMustWaitForPrimeDistribution(role) && cell.poolDistributionUnlocked !== true) return false;
  return true;
}

export function mergedFicheActionsDisabledHint(
  role: Role | string,
  emp: MergedFicheActionContext,
  cell: { linkedTemplateId?: string | null; poolDistributionUnlocked?: boolean },
  hasDraftId: boolean,
): string {
  if (mergedFicheActionsEnabled(role, emp, cell, hasDraftId)) return '';
  if (emp.fillingStatus.trim().toLowerCase() !== 'complete') {
    return 'Fiche pilote non complète.';
  }
  if (!hasDraftId) return 'Enregistrez la fiche cellule et le brouillon pôle.';
  if (!(cell.linkedTemplateId ?? '').trim()) {
    return 'Créez la partie commune (template) pour cette période.';
  }
  if (roleMustWaitForPrimeDistribution(role) && cell.poolDistributionUnlocked !== true) {
    return 'Disponible après toutes les validations PRIME (workflow fiches + synthèse globale).';
  }
  return 'Aperçu indisponible.';
}
