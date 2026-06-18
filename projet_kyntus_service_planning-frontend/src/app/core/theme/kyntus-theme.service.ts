import { Injectable, signal } from '@angular/core';

export type KyntusTheme = 'light' | 'dark';

export const KYNTHUS_THEME_STORAGE_KEY = 'kyntus_theme';

@Injectable({ providedIn: 'root' })
export class KyntusThemeService {
  readonly theme = signal<KyntusTheme>('light');

  constructor() {
    if (typeof document !== 'undefined') {
      const stored = localStorage.getItem(KYNTHUS_THEME_STORAGE_KEY) as KyntusTheme | null;
      const legacyPrime = localStorage.getItem('prime_theme') as KyntusTheme | null;
      const initial = stored ?? legacyPrime ?? 'light';
      this.applyTheme(initial);
      if (!stored && legacyPrime) {
        localStorage.setItem(KYNTHUS_THEME_STORAGE_KEY, legacyPrime);
      }
    }
  }

  toggleTheme(): void {
    this.applyTheme(this.theme() === 'light' ? 'dark' : 'light');
  }

  setTheme(next: KyntusTheme): void {
    this.applyTheme(next);
  }

  applyTheme(next: KyntusTheme): void {
    this.theme.set(next);
    if (typeof document === 'undefined') return;

    const body = document.body;
    body.classList.remove('theme-light', 'theme-dark');
    body.classList.add(`theme-${next}`);

    const root = document.documentElement;
    root.classList.toggle('dark', next === 'dark');

    localStorage.setItem(KYNTHUS_THEME_STORAGE_KEY, next);
  }
}

export function kyntusThemeInitFactory(theme: KyntusThemeService): () => void {
  return () => theme.applyTheme(theme.theme());
}
