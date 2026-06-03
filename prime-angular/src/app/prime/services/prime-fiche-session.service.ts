import { Injectable, computed, inject, signal } from '@angular/core';
import type { PrimeFicheTemplateSchema } from '../models/prime-fiche-template.schema';
import type { StoredPrimeTemplate } from '../models/prime-template.model';
import {
  loadStoredTemplates,
  parseTemplateCalcSnapshotV1,
  storedTemplateFromCalcSnapshotForPreview,
} from '../models/prime-template.model';
import type { SupervisorPolePrimeDraftDto } from './prime-cell-prime-api.service';
import { PrimeFicheTemplateActiveService } from './prime-fiche-template-active.service';

export type PrimeFicheWizardStep = 'idle' | 'setup' | 'preview' | 'entry' | 'result' | 'submitted';

function defaultPreviousMonth(): { year: number; month: number } {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() - 1);
  return { year: d.getFullYear(), month: d.getMonth() + 1 };
}

@Injectable({ providedIn: 'root' })
export class PrimeFicheSessionService {
  private readonly activeTpl = inject(PrimeFicheTemplateActiveService);

  /** Flux superviseur : idle = pas de wizard (ex. autre rôle ou mode legacy). */
  readonly step = signal<PrimeFicheWizardStep>('idle');

  readonly periodYear = signal(defaultPreviousMonth().year);
  readonly periodMonth = signal(defaultPreviousMonth().month);

  readonly selectedTemplateId = signal<string | null>(null);
  /** Copie du template choisi pour aperçu / recalcul (hors localStorage mutable). */
  readonly sessionTemplate = signal<StoredPrimeTemplate | null>(null);
  /** Schéma grille actif pour la fiche en cours (saisie + résultat). */
  readonly sessionSchema = signal<PrimeFicheTemplateSchema | null>(null);

  /** Incrémenté à chaque entrée en saisie (assistant) — pour réinitialiser hydratation / auto-aperçu. */
  readonly entryEpoch = signal(0);

  /** Dernier brouillon pôle persisté (API). */
  readonly polePrimeDraftId = signal<string | null>(null);

  /** Incrémenté après sauvegarde brouillon / retour liste pour rafraîchir la liste « fiches communes ». */
  readonly draftListBump = signal(0);

  bumpDraftListRefresh(): void {
    this.draftListBump.update((n) => n + 1);
  }

  /** Schéma effectif pour la saisie : session en priorité, sinon template global actif. */
  readonly effectiveSchema = computed(() => this.sessionSchema() ?? this.activeTpl.schema());

  /** Indique si le flux wizard superviseur est actif (étapes avant/après saisie structurée). */
  readonly useWizardFlow = computed(() => this.step() !== 'idle');

  /** Après « mode sans assistant », ne pas relancer automatiquement le wizard tant que l’utilisateur ne reprend pas le flux. */
  readonly preferLegacySaisie = signal(false);

  startWizardForSupervisor(): void {
    const d = defaultPreviousMonth();
    this.periodYear.set(d.year);
    this.periodMonth.set(d.month);
    this.selectedTemplateId.set(null);
    this.sessionTemplate.set(null);
    this.sessionSchema.set(null);
    this.polePrimeDraftId.set(null);
    this.entryEpoch.set(0);
    this.preferLegacySaisie.set(false);
    this.step.set('setup');
  }

  exitWizardToLegacy(): void {
    this.step.set('idle');
    this.sessionTemplate.set(null);
    this.sessionSchema.set(null);
    this.selectedTemplateId.set(null);
    this.preferLegacySaisie.set(true);
  }

  /**
   * Retour à la liste des fiches communes — `step = 'idle'`, sans marquer le mode legacy.
   * Permet au bouton « Retour à la liste » du wizard de réafficher `PrimeFichesCommunesListComponent`
   * (rendu conditionnel dans `prime-layout` quand step==='idle').
   */
  exitWizardToList(): void {
    this.step.set('idle');
    this.sessionTemplate.set(null);
    this.sessionSchema.set(null);
    this.selectedTemplateId.set(null);
    this.polePrimeDraftId.set(null);
    this.preferLegacySaisie.set(false);
    this.bumpDraftListRefresh();
  }

  /** Retour idle sans marquer « préférer le legacy » (ex. changement de rôle). */
  forceIdle(): void {
    this.step.set('idle');
    this.sessionTemplate.set(null);
    this.sessionSchema.set(null);
    this.selectedTemplateId.set(null);
    this.preferLegacySaisie.set(false);
  }

  /**
   * Ouvre le wizard sur un draft pôle existant (clic « Ouvrir » dans la liste).
   * - Reconstruit le `StoredPrimeTemplate` depuis le snapshot serveur (`templateCalcSnapshotJson` + `schemaJson`)
   *   pour garantir un aperçu fonctionnel même si le template n'est plus stocké en localStorage.
   * - Positionne période, templateId, sessionTemplate et polePrimeDraftId puis saute à l'étape `preview`.
   */
  startWizardFromExistingDraft(draft: SupervisorPolePrimeDraftDto): boolean {
    const periodMatch = /^(\d{4})-(\d{2})$/.exec((draft.period ?? '').trim());
    if (!periodMatch) return false;
    const y = Number(periodMatch[1]);
    const m = Number(periodMatch[2]);
    if (!Number.isFinite(y) || !Number.isFinite(m)) return false;

    let schema: PrimeFicheTemplateSchema | null = null;
    try {
      schema = JSON.parse(draft.schemaJson ?? '{}') as PrimeFicheTemplateSchema;
    } catch {
      schema = null;
    }
    if (!schema?.lines?.length) return false;

    const snap = parseTemplateCalcSnapshotV1(draft.templateCalcSnapshotJson ?? null);
    let tpl: StoredPrimeTemplate | null = null;
    if (snap) {
      tpl = storedTemplateFromCalcSnapshotForPreview(snap, schema, draft.templateId);
      tpl.displayName = draft.templateDisplayName || tpl.displayName;
    } else {
      // Fallback : template stocké en localStorage (anciens drafts sans snapshot serveur).
      tpl = loadStoredTemplates().find((t) => t.id === draft.templateId) ?? null;
      if (tpl) tpl = { ...tpl, ficheGridSchema: schema };
    }
    if (!tpl) return false;

    this.periodYear.set(y);
    this.periodMonth.set(m);
    this.selectedTemplateId.set(draft.templateId);
    this.sessionTemplate.set(tpl);
    this.sessionSchema.set(null);
    this.polePrimeDraftId.set(draft.id);
    this.preferLegacySaisie.set(false);
    this.entryEpoch.set(0);
    this.step.set('preview');
    return true;
  }

  setSelectedTemplateId(id: string | null): void {
    this.selectedTemplateId.set(id);
    const list = loadStoredTemplates();
    const t = id ? list.find((x) => x.id === id) ?? null : null;
    this.sessionTemplate.set(t);
  }

  /** Template courant hors liste locale (ex. import Excel direct partie commune). */
  setSessionTemplateFromDirectUpload(tpl: StoredPrimeTemplate): void {
    this.selectedTemplateId.set(tpl.id);
    this.sessionTemplate.set(tpl);
  }

  goPreview(): void {
    if (!this.selectedTemplateId() || !this.sessionTemplate()) return;
    this.step.set('preview');
  }

  goBackToSetup(): void {
    if (this.step() === 'preview') this.step.set('setup');
  }

  goEntry(): void {
    const tpl = this.sessionTemplate();
    const schema = tpl?.ficheGridSchema ?? null;
    if (!schema) return;
    // Copie profonde : évite toute mutation partagée avec le template listé / localStorage,
    // et garantit que secteurs.defaults restent bien attachés à la saisie en cours.
    const clone =
      typeof structuredClone === 'function'
        ? structuredClone(schema)
        : (JSON.parse(JSON.stringify(schema)) as PrimeFicheTemplateSchema);
    this.sessionSchema.set(clone);
    this.activeTpl.setActiveSchema(clone);
    this.step.set('entry');
    this.entryEpoch.update((n) => n + 1);
  }

  setPolePrimeDraftId(id: string | null): void {
    this.polePrimeDraftId.set(id);
  }

  goResult(): void {
    if (this.step() !== 'entry') return;
    this.step.set('result');
  }

  backToEntry(): void {
    if (this.step() === 'result') this.step.set('entry');
  }

  markSubmitted(): void {
    this.step.set('submitted');
  }

  /** Nouvelle fiche après enregistrement : retour à l’étape configuration. */
  restartWizard(): void {
    this.startWizardForSupervisor();
  }

  periodLabel(): string {
    const y = this.periodYear();
    const m = String(this.periodMonth()).padStart(2, '0');
    return `${y}-${m}`;
  }
}
