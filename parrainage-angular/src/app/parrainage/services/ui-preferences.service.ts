import { Injectable } from '@angular/core';

const STORAGE = 'parrainage.ui.prefs.v1';

export interface UiPreferences {
  compactMode: boolean;
}

@Injectable({ providedIn: 'root' })
export class UiPreferencesService {
  get(): UiPreferences {
    try {
      const raw = localStorage.getItem(STORAGE);
      if (!raw) return { compactMode: false };
      return { compactMode: false, ...JSON.parse(raw) };
    } catch {
      return { compactMode: false };
    }
  }

  set(partial: Partial<UiPreferences>): void {
    const next = { ...this.get(), ...partial };
    localStorage.setItem(STORAGE, JSON.stringify(next));
    window.dispatchEvent(new CustomEvent('parrainage:ui-prefs'));
  }
}
