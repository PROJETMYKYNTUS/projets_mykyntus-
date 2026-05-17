import { Injectable, signal } from '@angular/core';
import type { PrimeFicheTemplateSchema } from '../models/prime-fiche-template.schema';
import {
  PRIME_FICHE_TEMPLATE_FORMAT_V1,
  PRIME_FICHE_TEMPLATE_FORMAT_V2,
} from '../models/prime-fiche-template.schema';

const ACTIVE_TEMPLATE_STORAGE_KEY = 'prime:fiche-template-active:v1';

function isSupportedSchema(x: unknown): x is PrimeFicheTemplateSchema {
  if (!x || typeof x !== 'object') return false;
  const o = x as Record<string, unknown>;
  const v = o['templateFormatVersion'];
  return (v === PRIME_FICHE_TEMPLATE_FORMAT_V1 || v === PRIME_FICHE_TEMPLATE_FORMAT_V2) && Array.isArray(o['lines']);
}

@Injectable({ providedIn: 'root' })
export class PrimeFicheTemplateActiveService {
  readonly schema = signal<PrimeFicheTemplateSchema | null>(this.loadFromStorage());

  setActiveSchema(schema: PrimeFicheTemplateSchema): void {
    this.schema.set(schema);
    try {
      localStorage.setItem(ACTIVE_TEMPLATE_STORAGE_KEY, JSON.stringify(schema));
    } catch {
      // quota / private mode
    }
  }

  clearActive(): void {
    this.schema.set(null);
    try {
      localStorage.removeItem(ACTIVE_TEMPLATE_STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  private loadFromStorage(): PrimeFicheTemplateSchema | null {
    try {
      const raw = localStorage.getItem(ACTIVE_TEMPLATE_STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as unknown;
      return isSupportedSchema(parsed) ? parsed : null;
    } catch {
      return null;
    }
  }
}
