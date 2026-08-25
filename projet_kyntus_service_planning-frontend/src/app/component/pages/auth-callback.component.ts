import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RedirectService } from '../../core/services/redirect.service';
import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import { KyntusNotificationInitService } from '../../core/notifications/kyntus-notification-init.service';
import { persistAccessTokens, clearStoredTokens, decodeJwtPayload, readJwtEmail, readJwtName, readJwtNameIdentifier, readJwtRole } from '../../core/session/kyntus-auth-token.util';
import { redirectToAuthLogin } from '../../core/session/kyntus-auth-refresh.service';
import { persistReturnUrl } from '../../core/session/kyntus-return-url.util';
import { KyntusThemeService, type KyntusTheme } from '../../core/theme/kyntus-theme.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="auth-callback">
      <div class="auth-callback-spinner"></div>
      <p class="auth-callback-text">Chargement en cours...</p>
    </div>
  `,
  styles: [`
    .auth-callback {
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      height: 100vh;
      gap: 12px;
      font-family: 'Inter', ui-sans-serif, system-ui, sans-serif;
      background: var(--bg-primary, #f8fafc);
    }
    .auth-callback-spinner {
      width: 32px;
      height: 32px;
      border: 3px solid var(--border-color, #e5e7eb);
      border-top-color: var(--soft-blue, #3b82f6);
      border-radius: 50%;
      animation: auth-callback-spin 0.8s linear infinite;
    }
    .auth-callback-text {
      color: var(--text-muted, #6b7280);
      font-size: 14px;
    }
    @keyframes auth-callback-spin {
      to { transform: rotate(360deg); }
    }
  `],
})
export class AuthCallbackComponent implements OnInit {

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private redirectService: RedirectService,
    private readonly documentationIdentity: DocumentationIdentityService,
    private readonly notificationInit: KyntusNotificationInitService,
    private readonly themeService: KyntusThemeService,
  ) {}

  ngOnInit(): void {
    const token   = this.route.snapshot.queryParams['token'];
    const refresh = this.route.snapshot.queryParams['refresh'];
    const returnUrl = this.route.snapshot.queryParams['returnUrl'] as string | undefined;
    persistReturnUrl(returnUrl);
    const themeParam = this.route.snapshot.queryParams['theme'];
    if (themeParam === 'light' || themeParam === 'dark') {
      this.themeService.setTheme(themeParam as KyntusTheme);
    }

    if (!token) {
      redirectToAuthLogin(returnUrl);
      return;
    }

    persistAccessTokens(token, refresh ?? undefined);

    try {
      const payload = decodeJwtPayload(token);
      if (!payload) throw new Error('JWT payload illisible');

      const role = readJwtRole(token);
      const nameIdentifier = readJwtNameIdentifier(token);
      const sub = payload['sub'];
      const authUserIdRaw = nameIdentifier ? parseInt(String(nameIdentifier), 10) : NaN;
      const authUserId = Number.isFinite(authUserIdRaw) && authUserIdRaw > 0 ? authUserIdRaw : 0;
      const subjectId =
        typeof sub === 'string' && sub.trim().length > 0
          ? sub.trim()
          : nameIdentifier.trim() !== ''
            ? nameIdentifier.trim()
            : '';
      const username = readJwtName(token) || 'Utilisateur';
      const email = readJwtEmail(token);

      localStorage.setItem('user', JSON.stringify({
        id: subjectId,
        authUserId,
        subjectId,
        guid: subjectId,
        username,
        email,
        role
      }));

      this.documentationIdentity.syncFromJwtSession();
      this.notificationInit.connectIfAuthenticated();

      // Retire tokens de l’URL / historique avant navigation (replaceUrl).
      // returnUrl est déjà capturé depuis la query avant ce scrub.
      if (typeof history !== 'undefined') {
        history.replaceState(null, '', '/auth-callback');
      }
      setTimeout(() => this.redirectService.redirectAfterLogin(returnUrl), 100);

    } catch (e) {
      console.warn('Impossible de décoder le token JWT :', e);
      clearStoredTokens();
      redirectToAuthLogin(returnUrl);
    }
  }
}
