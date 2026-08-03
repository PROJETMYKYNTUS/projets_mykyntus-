import { Injectable, OnDestroy } from '@angular/core';
import { registerAuthDraftFlusher } from '../session/kyntus-auth-refresh.service';

const DRAFT_PREFIX = 'kyntus.formDraft.v1:';
const DRAFT_TTL_MS = 24 * 60 * 60 * 1000;

interface DraftEnvelope<T = unknown> {
  version: 1;
  savedAt: string;
  payload: T;
}

type PendingFlush = () => void;

@Injectable({ providedIn: 'root' })
export class KyntusFormDraftService implements OnDestroy {
  private readonly pending = new Map<string, PendingFlush>();

  constructor() {
    registerAuthDraftFlusher(() => this.flushAllPending());
  }

  ngOnDestroy(): void {
    registerAuthDraftFlusher(null);
  }

  private userScope(): string {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return 'anon';
      const user = JSON.parse(raw) as { subjectId?: string; id?: string; authUserId?: number };
      return String(user.subjectId || user.id || user.authUserId || 'anon');
    } catch {
      return 'anon';
    }
  }

  private storageKey(draftKey: string): string {
    return `${DRAFT_PREFIX}${this.userScope()}:${draftKey}`;
  }

  save<T>(draftKey: string, payload: T): void {
    if (typeof sessionStorage === 'undefined' || !draftKey) return;
    const envelope: DraftEnvelope<T> = {
      version: 1,
      savedAt: new Date().toISOString(),
      payload,
    };
    try {
      sessionStorage.setItem(this.storageKey(draftKey), JSON.stringify(envelope));
    } catch {
      // quota / private mode — ignorer silencieusement
    }
  }

  load<T>(draftKey: string): T | null {
    if (typeof sessionStorage === 'undefined' || !draftKey) return null;
    try {
      const raw = sessionStorage.getItem(this.storageKey(draftKey));
      if (!raw) return null;
      const envelope = JSON.parse(raw) as DraftEnvelope<T>;
      if (envelope.version !== 1 || envelope.payload == null) {
        this.clear(draftKey);
        return null;
      }
      const savedAt = Date.parse(envelope.savedAt);
      if (!Number.isFinite(savedAt) || Date.now() - savedAt > DRAFT_TTL_MS) {
        this.clear(draftKey);
        return null;
      }
      return envelope.payload;
    } catch {
      this.clear(draftKey);
      return null;
    }
  }

  clear(draftKey: string): void {
    if (typeof sessionStorage === 'undefined' || !draftKey) return;
    sessionStorage.removeItem(this.storageKey(draftKey));
    this.pending.delete(draftKey);
  }

  /** Enregistre un flush synchrone (debounce directive) avant redirect idle/401. */
  registerPendingFlush(draftKey: string, flush: PendingFlush): void {
    this.pending.set(draftKey, flush);
  }

  unregisterPendingFlush(draftKey: string): void {
    this.pending.delete(draftKey);
  }

  flushAllPending(): void {
    for (const flush of [...this.pending.values()]) {
      try {
        flush();
      } catch {
        // ignore
      }
    }
  }
}
