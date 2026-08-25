import { computed, inject, Injectable, signal } from '@angular/core';
import type { PrimeFicheTemplateSchema } from '../models/prime-fiche-template.schema';
import type { StoredPrimeTemplate } from '../models/prime-template.model';
import {
  loadStoredTemplates,
  parseTemplateCalcSnapshotV1,
  storedTemplateFromCalcSnapshotForPreview,
} from '../models/prime-template.model';
import type { SupervisorPolePrimeDraftDto } from './prime-cell-prime-api.service';
import { PrimeFicheTemplateActiveService } from './prime-fiche-template-active.service';
import { PrimeScopeStore } from '../state/prime-scope.store';
import { RoleService } from '../state/role.service';

export type PrimeFicheWizardStep =
  | 'idle'
  | 'setup'
  | 'ponderations'
  | 'preview'
  | 'entry'
  | 'result'
  | 'submitted';

@Injectable({ providedIn: 'root' })
export class PrimeFicheSessionService {
  private readonly activeTpl = inject(PrimeFicheTemplateActiveService);
  private readonly scope = inject(PrimeScopeStore);
  private readonly role = inject(RoleService);

  /** Flux superviseur : idle = pas de wizard (ex. autre rôle ou mode legacy). */
  readonly step = signal<PrimeFicheWizardStep>('idle');

  readonly periodYear = computed(() => this.scope.periodYear());
  readonly periodMonth = computed(() => this.scope.periodMonth());

  readonly selectedTemplateId = computed(() => {
    const id = this.scope.selectedTemplateId().trim();
    return id || null;
  });

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

  private currentUserId(): string {
    return (this.role.currentUser()?.id ?? '').trim();
  }

  startWizardForSupervisor(): void {
    const uid = this.currentUserId();
    if (uid) this.scope.hydrateFromStorage(uid);
    this.scope.setSelectedTemplateId(null, uid);
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
    this.scope.setSelectedTemplateId(null, this.currentUserId());
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
    this.scope.setSelectedTemplateId(null, this.currentUserId());
    this.polePrimeDraftId.set(null);
    this.preferLegacySaisie.set(false);
    this.bumpDraftListRefresh();
  }

  /** Retour idle sans marquer « préférer le legacy » (ex. changement de rôle). */
  forceIdle(): void {
    this.step.set('idle');
    this.sessionTemplate.set(null);
    this.sessionSchema.set(null);
    this.scope.setSelectedTemplateId(null, this.currentUserId());
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
      tpl = loadStoredTemplates().find((t) => t.id === draft.templateId) ?? null;
      if (tpl) tpl = { ...tpl, ficheGridSchema: schema };
    }
    if (!tpl) return false;

    const uid = this.currentUserId();
    this.scope.setPeriodParts(y, m, uid);
    const celluleId = (draft.celluleId ?? draft.poleId ?? '').trim();
    if (celluleId) this.scope.setSelectedCelluleId(celluleId, uid);
    this.scope.setSelectedTemplateId(draft.templateId, uid);
    this.sessionTemplate.set(tpl);
    this.sessionSchema.set(null);
    this.polePrimeDraftId.set(draft.id);
    this.preferLegacySaisie.set(false);
    this.entryEpoch.set(0);
    this.step.set('ponderations');
    return true;
  }

  setSelectedTemplateId(id: string | null): void {
    const uid = this.currentUserId();
    this.scope.setSelectedTemplateId(id, uid);
    const list = loadStoredTemplates();
    const t = id ? list.find((x) => x.id === id) ?? null : null;
    this.sessionTemplate.set(t);
  }

  setPeriodMonth(month: number): void {
    const uid = this.currentUserId();
    this.scope.setPeriodParts(this.scope.periodYear(), month, uid);
  }

  setPeriodYear(year: number): void {
    const uid = this.currentUserId();
    this.scope.setPeriodParts(year, this.scope.periodMonth(), uid);
  }

  /** Template courant hors liste locale (ex. import Excel direct partie commune). */
  setSessionTemplateFromDirectUpload(tpl: StoredPrimeTemplate): void {
    this.scope.setSelectedTemplateId(tpl.id, this.currentUserId());
    this.sessionTemplate.set(tpl);
  }

  goPreview(): void {
    this.goPonderations();
  }

  /** Après période / modèle : étape indicateurs & pondérations (wizard). */
  goPonderations(): void {
    if (!this.selectedTemplateId() || !this.sessionTemplate()?.ficheGridSchema) return;
    this.step.set('ponderations');
  }

  goBackToSetup(): void {
    if (this.step() === 'preview' || this.step() === 'ponderations') this.step.set('setup');
  }

  goBackToPonderations(): void {
    if (this.step() === 'entry') this.step.set('ponderations');
  }

  goEntry(): void {
    const tpl = this.sessionTemplate();
    const schema = tpl?.ficheGridSchema ?? null;
    if (!schema) return;
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
    return this.scope.period();
  }
}
