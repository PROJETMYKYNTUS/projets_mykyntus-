import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { redirectToAuthLogin } from '../../../core/session/kyntus-auth-refresh.service';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="unauthorized ky-fade-up">
      <h1 class="unauthorized-code">403</h1>
      <p class="unauthorized-text">Vous n'avez pas accès à cette page.</p>
      <div class="unauthorized-actions">
        <a class="ky-btn-secondary" routerLink="/home">Accueil</a>
        <button type="button" class="ky-btn-primary" (click)="goLogin()">Retour au login</button>
      </div>
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
    .unauthorized-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      justify-content: center;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnauthorizedComponent {
  goLogin(): void {
    redirectToAuthLogin();
  }
}
