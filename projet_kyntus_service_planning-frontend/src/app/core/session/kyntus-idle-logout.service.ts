import { Injectable, inject, NgZone } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { redirectToAuthLogin } from './kyntus-auth-refresh.service';
import { readStoredAccessToken, readStoredRefreshToken } from './kyntus-auth-token.util';
import { currentAppReturnUrl } from './kyntus-return-url.util';
import { KyntusToastService } from '../../shared/components/ui/kyntus-toast.service';
import { KyntusFormDraftService } from '../drafts/kyntus-form-draft.service';

/** Silence avant avertissement (activité réelle : souris, clavier, scroll, navigation…). */
export const KYNTUS_IDLE_SILENCE_MS = 5 * 60_000;
/** Durée de l’avertissement avant déconnexion. */
export const KYNTUS_IDLE_WARNING_MS = 60_000;
/** Throttle des events d’activité (évite de reset le timer à chaque pixel). */
const ACTIVITY_THROTTLE_MS = 1_000;

const ACTIVITY_EVENTS: (keyof WindowEventMap)[] = [
  'mousemove',
  'mousedown',
  'keydown',
  'scroll',
  'touchstart',
  'wheel',
  'focus',
];

@Injectable({ providedIn: 'root' })
export class KyntusIdleLogoutService {
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly toast = inject(KyntusToastService);
  private readonly drafts = inject(KyntusFormDraftService);

  private silenceTimer: ReturnType<typeof setTimeout> | null = null;
  private warningTimer: ReturnType<typeof setTimeout> | null = null;
  private started = false;
  private warningActive = false;
  private lastBumpAt = 0;
  private readonly onActivity = (): void => this.throttledBump();

  /** À appeler une fois au démarrage de l’app (APP_INITIALIZER). */
  start(): void {
    if (this.started || typeof window === 'undefined') return;
    this.started = true;

    // Assure l’enregistrement du flusher drafts (providedIn root).
    void this.drafts;

    this.zone.runOutsideAngular(() => {
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(() => this.bump());

      for (const evt of ACTIVITY_EVENTS) {
        window.addEventListener(evt, this.onActivity, { passive: true, capture: true });
      }

      // Cache navigateur (bfcache) : après déconnexion, Retour ne doit pas restaurer la SPA.
      window.addEventListener('pageshow', (ev: PageTransitionEvent) => {
        if (ev.persisted && !this.hasSession()) {
          this.zone.run(() => redirectToAuthLogin(currentAppReturnUrl() ?? undefined));
        }
      });
    });

    this.bump();
  }

  /** Réinitialise le cycle idle (activité détectée). */
  bump(): void {
    if (!this.hasSession()) {
      this.clearTimers();
      this.warningActive = false;
      return;
    }

    this.lastBumpAt = Date.now();

    if (this.warningActive) {
      this.warningActive = false;
      this.zone.run(() => this.toast.dismiss());
    }

    this.clearTimers();
    this.zone.runOutsideAngular(() => {
      this.silenceTimer = setTimeout(() => this.onSilenceElapsed(), KYNTUS_IDLE_SILENCE_MS);
    });
  }

  private throttledBump(): void {
    if (this.warningActive) {
      this.bump();
      return;
    }
    const now = Date.now();
    if (now - this.lastBumpAt < ACTIVITY_THROTTLE_MS) return;
    this.bump();
  }

  private onSilenceElapsed(): void {
    if (!this.hasSession()) return;
    this.warningActive = true;
    this.zone.run(() => {
      this.toast.info(
        'Session inactive — déconnexion dans 1 minute. Bougez ou cliquez pour rester connecté.',
        KYNTUS_IDLE_WARNING_MS,
      );
    });
    this.zone.runOutsideAngular(() => {
      this.warningTimer = setTimeout(() => this.onIdle(), KYNTUS_IDLE_WARNING_MS);
    });
  }

  private onIdle(): void {
    if (!this.hasSession()) return;
    this.warningActive = false;
    this.clearTimers();
    this.zone.run(() => {
      this.toast.dismiss();
      this.drafts.flushAllPending();
      redirectToAuthLogin(currentAppReturnUrl() ?? undefined);
    });
  }

  private hasSession(): boolean {
    return !!(readStoredAccessToken() || readStoredRefreshToken());
  }

  private clearTimers(): void {
    if (this.silenceTimer) {
      clearTimeout(this.silenceTimer);
      this.silenceTimer = null;
    }
    if (this.warningTimer) {
      clearTimeout(this.warningTimer);
      this.warningTimer = null;
    }
  }
}

export function kyntusIdleLogoutInitFactory(idle: KyntusIdleLogoutService): () => void {
  return () => idle.start();
}
