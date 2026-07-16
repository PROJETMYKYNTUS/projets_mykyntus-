import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RedirectService } from '../../core/services/redirect.service';
import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import { KyntusNotificationInitService } from '../../core/notifications/kyntus-notification-init.service';
import { KYNTUS_PUBLIC_URLS } from '../../config/kyntus-public-urls';
import { persistAccessTokens, clearStoredTokens } from '../../core/session/kyntus-auth-token.util';

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

  // ── Claims JWT ──────────────────────────────────────────────
  private readonly ROLE_CLAIM  = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
  private readonly ID_CLAIM    = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
  private readonly NAME_CLAIM  = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
  private readonly EMAIL_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private redirectService: RedirectService,
    private readonly documentationIdentity: DocumentationIdentityService,
    private readonly notificationInit: KyntusNotificationInitService,
  ) {}

  ngOnInit(): void {
    const token   = this.route.snapshot.queryParams['token'];
    const refresh = this.route.snapshot.queryParams['refresh'];

    // ── Pas de token → retour login ─────────────────────────
    if (!token) {
      window.location.href = KYNTUS_PUBLIC_URLS.authLogin;
      return;
    }

    // ── Stocker les tokens ───────────────────────────────────
    persistAccessTokens(token, refresh ?? undefined);

    // ── Décoder le JWT et sauvegarder le user ────────────────
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));

      const role     = payload[this.ROLE_CLAIM]  || '';
      const nameIdentifier = payload[this.ID_CLAIM];
      const sub = payload['sub'];
      const authUserIdRaw = nameIdentifier != null ? parseInt(String(nameIdentifier), 10) : NaN;
      const authUserId = Number.isFinite(authUserIdRaw) && authUserIdRaw > 0 ? authUserIdRaw : 0;
      const subjectId =
        typeof sub === 'string' && sub.trim().length > 0
          ? sub.trim()
          : nameIdentifier != null && String(nameIdentifier).trim() !== ''
            ? String(nameIdentifier).trim()
            : '';
      const username = payload[this.NAME_CLAIM]  || 'Utilisateur';
      const email    = payload[this.EMAIL_CLAIM] || '';

      // ✅ Debug — à supprimer en production
      console.log('=== AuthCallback ===');
      console.log('Payload JWT :', payload);
      console.log('Rôle détecté :', role);
      console.log('User :', { authUserId, subjectId, username, email });

      // ✅ Sauvegarder le user complet
      localStorage.setItem('user', JSON.stringify({
        id: subjectId,
        authUserId,
        subjectId,
        username,
        email,
        role
      }));

      this.documentationIdentity.syncFromJwtSession();
      this.notificationInit.connectIfAuthenticated();

      // ✅ UNE seule redirection — RedirectService décide selon le rôle
      setTimeout(() => this.redirectService.redirectAfterLogin(), 100);

    } catch (e) {
      // JWT invalide → fallback dashboard-employee
      console.warn('Impossible de décoder le token JWT :', e);
      clearStoredTokens();
      setTimeout(() => this.router.navigate(['/dashboard-employee']), 100);
    }
  }
}