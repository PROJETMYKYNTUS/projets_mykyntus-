import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RedirectService } from '../../core/services/redirect.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display:flex; flex-direction:column; justify-content:center; 
                align-items:center; height:100vh; gap:12px; font-family:sans-serif;">
      <div style="width:32px; height:32px; border:3px solid #e5e7eb; 
                  border-top-color:#6366f1; border-radius:50%; animation:spin 0.8s linear infinite;"></div>
      <p style="color:#6b7280; font-size:14px;">Chargement en cours...</p>
      <style>@keyframes spin { to { transform: rotate(360deg); } }</style>
    </div>
  `
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
    private redirectService: RedirectService
  ) {}

  ngOnInit(): void {
    const token   = this.route.snapshot.queryParams['token'];
    const refresh = this.route.snapshot.queryParams['refresh'];

    // ── Pas de token → retour login ─────────────────────────
    if (!token) {
      window.location.href = 'http://localhost:4201/login';
      return;
    }

    // ── Stocker les tokens ───────────────────────────────────
    localStorage.setItem('token', token);
    if (refresh) {
      localStorage.setItem('refreshToken', refresh);
    }

    // ── Décoder le JWT et sauvegarder le user ────────────────
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));

      const role     = payload[this.ROLE_CLAIM]  || '';
      const userId   = parseInt(payload[this.ID_CLAIM]) || 0;
      const username = payload[this.NAME_CLAIM]  || 'Utilisateur';
      const email    = payload[this.EMAIL_CLAIM] || '';

      // ✅ Debug — à supprimer en production
      console.log('=== AuthCallback ===');
      console.log('Payload JWT :', payload);
      console.log('Rôle détecté :', role);
      console.log('User :', { userId, username, email });

      // ✅ Sauvegarder le user complet
      localStorage.setItem('user', JSON.stringify({
        id: userId,
        username,
        email,
        role
      }));

      // ✅ UNE seule redirection — RedirectService décide selon le rôle
      setTimeout(() => this.redirectService.redirectAfterLogin(), 100);

    } catch (e) {
      // JWT invalide → fallback dashboard-employee
      console.warn('Impossible de décoder le token JWT :', e);
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      setTimeout(() => this.router.navigate(['/dashboard-employee']), 100);
    }
  }
}