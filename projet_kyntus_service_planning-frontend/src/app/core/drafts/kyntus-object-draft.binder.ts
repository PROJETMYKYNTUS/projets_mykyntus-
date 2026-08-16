import { KyntusFormDraftService } from './kyntus-form-draft.service';

/**
 * Persistance brouillon pour état objet / ngModel (hors FormGroup).
 * Restaure au start, flush avant idle/401, clear après submit réussi / reset.
 * Après clear/discard, destroy() ne réécrit plus le brouillon.
 */
export class KyntusObjectDraftBinder<T> {
  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private started = false;
  private discarded = false;

  constructor(
    private readonly drafts: KyntusFormDraftService,
    private readonly draftKey: string,
    private readonly getState: () => T,
    private readonly applyState: (state: T) => void,
  ) {}

  start(): void {
    if (this.started || !this.draftKey) return;
    this.started = true;
    this.discarded = false;

    const saved = this.drafts.load<T>(this.draftKey);
    if (saved != null) {
      this.applyState(saved);
    }

    this.drafts.registerPendingFlush(this.draftKey, () => {
      this.flushNow();
    });
  }

  /** À appeler sur chaque changement significatif (ngModelChange, etc.). */
  touch(): void {
    if (!this.started || this.discarded) return;
    if (this.debounceTimer) clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => this.flushNow(), 500);
  }

  flushNow(): void {
    if (this.discarded || !this.started) return;
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.drafts.save(this.draftKey, this.getState());
  }

  /** Efface le brouillon (submit réussi) et empêche un re-flush au destroy. */
  clear(): void {
    this.discard();
  }

  /** Jette le brouillon sans le réécrire (reset manuel / submit réussi). */
  discard(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.discarded = true;
    this.drafts.clear(this.draftKey);
  }

  destroy(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    if (this.started) {
      if (!this.discarded) {
        this.flushNow();
      }
      this.drafts.unregisterPendingFlush(this.draftKey);
    }
    this.started = false;
  }
}
