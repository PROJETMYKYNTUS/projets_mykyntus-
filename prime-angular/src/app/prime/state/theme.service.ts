import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'prime_theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>('light');

  constructor() {
    const stored = (localStorage.getItem(THEME_STORAGE_KEY) as Theme) || 'light';
    this.applyTheme(stored);
  }

  toggleTheme(): void {
    this.applyTheme(this.theme() === 'light' ? 'dark' : 'light');
  }

  setTheme(next: Theme): void {
    this.applyTheme(next);
  }

  private applyTheme(next: Theme): void {
    this.theme.set(next);

    const body = document.body;
    body.classList.remove('theme-light', 'theme-dark');
    body.classList.add(`theme-${next}`);

    const root = document.documentElement;
    if (next === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }

    localStorage.setItem(THEME_STORAGE_KEY, next);
  }
}
