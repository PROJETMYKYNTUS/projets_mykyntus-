import type { CampaignStepStatusDto } from '../services/prime-cell-prime-api.service';
import type { PrimeNavRequestService } from '../services/prime-nav-request.service';

/** Navigation interne Prime depuis un chemin campaign (avec query tab optionnelle). */
export function navigatePrimeCampaignPath(
  nav: PrimeNavRequestService,
  path: string | null | undefined,
): void {
  const raw = (path ?? '').trim();
  if (!raw) return;
  const [base, query] = raw.split('?');
  const tab = query ? new URLSearchParams(query).get('tab') : null;
  if (tab) {
    nav.requestViewWithTab(base, tab);
    return;
  }
  nav.requestView(base || raw);
}

/** Étape wizard saisie commune demandée depuis le campaign stepper / CTA. */
export type CampaignSaisieWizardStep = 'ponderations' | 'entry' | 'setup';

export function resolveCampaignSaisieWizardStep(
  stepKey: string | null | undefined,
  path: string | null | undefined,
  label: string | null | undefined,
): CampaignSaisieWizardStep | null {
  const raw = (path ?? '').trim();
  const base = raw.split('?')[0] || raw;
  const key = (stepKey ?? '').trim().toLowerCase();

  if (key === 'ponderations') return 'ponderations';
  if (key === 'common') return 'entry';

  if (base !== '/prime-saisie') return null;

  if (key === 'template') return 'setup';

  const blob = `${label ?? ''} ${raw}`.toLowerCase();
  if (blob.includes('pondér') || blob.includes('ponder')) return 'ponderations';
  if (blob.includes('commun') || blob.includes('saisie') || blob.includes('reconduire')) {
    return 'entry';
  }
  return 'ponderations';
}

export function navigateCampaignStep(
  nav: PrimeNavRequestService,
  step: CampaignStepStatusDto,
): void {
  navigatePrimeCampaignPath(nav, step.actionPath);
}
