import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KyntusThemeService } from './kyntus-theme.service';

@Component({
  selector: 'app-theme-toggle-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      type="button"
      class="theme-icon-btn"
      (click)="theme.toggleTheme()"
      [attr.aria-label]="theme.theme() === 'light' ? 'Activer le mode sombre' : 'Activer le mode clair'"
      [attr.title]="theme.theme() === 'light' ? 'Mode sombre' : 'Mode clair'"
    >
      @if (theme.theme() === 'light') {
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
        </svg>
      } @else {
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <circle cx="12" cy="12" r="4"/>
          <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/>
        </svg>
      }
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    .theme-icon-btn {
      position: relative;
      width: 36px;
      height: 36px;
      padding: 0;
      border: none;
      border-radius: 999px;
      background: transparent;
      color: var(--text-muted, var(--muted, #8b9bb0));
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      transition: background 0.18s ease, color 0.18s ease;
    }
    .theme-icon-btn:hover {
      background: color-mix(in srgb, var(--auth-signal, var(--signal, #3b82f6)) 14%, transparent);
      color: var(--auth-signal, var(--signal, #3b82f6));
    }
    .theme-icon-btn svg {
      width: 16px;
      height: 16px;
      display: block;
    }
  `],
})
export class ThemeToggleButtonComponent {
  readonly theme = inject(KyntusThemeService);
}
