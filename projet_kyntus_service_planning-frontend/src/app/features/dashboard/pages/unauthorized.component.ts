import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KYNTUS_PUBLIC_URLS } from '../../../config/kyntus-public-urls';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="unauthorized ky-fade-up">
      <h1 class="unauthorized-code">403</h1>
      <p class="unauthorized-text">Vous n'avez pas accès à cette page.</p>
      <a class="ky-btn-primary" [href]="authLoginUrl">Retour au login</a>
    </div>
  `,
  styles: [`
    .unauthorized {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      gap: 1rem;
      background: var(--bg-primary);
      text-align: center;
      padding: 1.5rem;
      font-family: var(--font-sans);
    }
    .unauthorized-code {
      margin: 0;
      font-size: 4rem;
      font-weight: 800;
      letter-spacing: -0.02em;
      color: var(--text-primary);
    }
    .unauthorized-text {
      margin: 0;
      font-size: 0.9375rem;
      color: var(--text-muted);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnauthorizedComponent {
  readonly authLoginUrl = KYNTUS_PUBLIC_URLS.authLogin;
}
