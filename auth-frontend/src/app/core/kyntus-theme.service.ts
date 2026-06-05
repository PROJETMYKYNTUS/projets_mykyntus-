import { Injectable, signal } from '@angular/core';

export type KyntusTheme = 'light' | 'dark';

export const KYNTHUS_THEME_STORAGE_KEY = 'kyntus_theme';

@Injectable({ providedIn: 'root' })
export class KyntusThemeService {
  readonly theme = signal<KyntusTheme>('light');

  constructor() {
    if (typeof document === 'undefined' || typeof localStorage === 'undefined') return;
    const stored = localStorage.getItem(KYNTHUS_THEME_STORAGE_KEY) as KyntusTheme | null;
    const legacy = localStorage.getItem('prime_theme') as KyntusTheme | null;
    const initial = stored ?? legacy ?? 'light';
    this.applyTheme(initial);
    if (!stored && legacy) {
      localStorage.setItem(KYNTHUS_THEME_STORAGE_KEY, legacy);
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

    document.body.classList.remove('theme-light', 'theme-dark');
    document.body.classList.add(`theme-${next}`);
    document.documentElement.classList.toggle('dark', next === 'dark');
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(KYNTHUS_THEME_STORAGE_KEY, next);
    }
  }
}

export function kyntusAuthThemeInit(theme: KyntusThemeService): () => void {
  return () => theme.applyTheme(theme.theme());
}
