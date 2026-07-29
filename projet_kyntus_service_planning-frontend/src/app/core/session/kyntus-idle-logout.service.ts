import { Injectable, inject, NgZone } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { redirectToAuthLogin } from './kyntus-auth-refresh.service';
import { readStoredAccessToken, readStoredRefreshToken } from './kyntus-auth-token.util';

/** Déconnexion automatique après inactivité de navigation (pas de timer absolu depuis le login). */
export const KYNTUS_IDLE_LOGOUT_MS = 5 * 60_000;

@Injectable({ providedIn: 'root' })
export class KyntusIdleLogoutService {
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);

  private timer: ReturnType<typeof setTimeout> | null = null;
  private started = false;

  /** À appeler une fois au démarrage de l’app (APP_INITIALIZER). */
  start(): void {
    if (this.started || typeof window === 'undefined') return;
    this.started = true;

    this.zone.runOutsideAngular(() => {
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(() => this.bump());

      // Cache navigateur (bfcache) : après déconnexion, Retour ne doit pas restaurer la SPA.
      window.addEventListener('pageshow', (ev: PageTransitionEvent) => {
        if (ev.persisted && !this.hasSession()) {
          this.zone.run(() => redirectToAuthLogin());
        }
      });
    });

    this.bump();
  }

  /** Réinitialise le délai de 5 minutes (navigation). */
  bump(): void {
    if (!this.hasSession()) {
      this.clearTimer();
      return;
    }
    this.clearTimer();
    this.zone.runOutsideAngular(() => {
      this.timer = setTimeout(() => this.onIdle(), KYNTUS_IDLE_LOGOUT_MS);
    });
  }

  private onIdle(): void {
    if (!this.hasSession()) return;
    this.zone.run(() => redirectToAuthLogin());
  }

  private hasSession(): boolean {
    return !!(readStoredAccessToken() || readStoredRefreshToken());
  }

  private clearTimer(): void {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}

export function kyntusIdleLogoutInitFactory(idle: KyntusIdleLogoutService): () => void {
  return () => idle.start();
}
