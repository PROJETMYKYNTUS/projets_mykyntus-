import { KyntusFormDraftService } from './kyntus-form-draft.service';

/**
 * Persistance brouillon pour état objet / ngModel (hors FormGroup).
 * Restaure au start, flush avant idle/401, clear après submit réussi.
 */
export class KyntusObjectDraftBinder<T> {
  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private started = false;

  constructor(
    private readonly drafts: KyntusFormDraftService,
    private readonly draftKey: string,
    private readonly getState: () => T,
    private readonly applyState: (state: T) => void,
  ) {}

  start(): void {
    if (this.started || !this.draftKey) return;
    this.started = true;

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
    if (!this.started) return;
    if (this.debounceTimer) clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => this.flushNow(), 500);
  }

  flushNow(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.drafts.save(this.draftKey, this.getState());
  }

  clear(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.drafts.clear(this.draftKey);
  }

  destroy(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    if (this.started) {
      this.flushNow();
      this.drafts.unregisterPendingFlush(this.draftKey);
    }
    this.started = false;
  }
}
