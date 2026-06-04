import { Injectable, computed, inject } from '@angular/core';
import {
  KyntusThemeService,
  type KyntusTheme,
} from '../../../core/theme/kyntus-theme.service';

export type Theme = KyntusTheme;

/** Délègue au thème global plateforme. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly kyntus = inject(KyntusThemeService);

  readonly theme = computed(() => this.kyntus.theme());

  toggleTheme(): void {
    this.kyntus.toggleTheme();
  }

  setTheme(next: Theme): void {
    this.kyntus.setTheme(next);
  }
}
