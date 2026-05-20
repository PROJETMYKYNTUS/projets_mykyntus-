import { Injectable, signal } from '@angular/core';

/**
 * Navigation interne au shell PRIME (sans routeur Angular) : demande de changement de vue
 * consommée par {@link PrimeLayoutComponent}.
 *
 * `requestedPeriod` est un canal optionnel pour transmettre une période (`YYYY-MM`) à la page
 * cible (ex. liste fiches communes → pilotage cellule), consommée par la page destinataire
 * via un `effect` puis remise à `null` avec {@link clearRequestedPeriod}.
 */
@Injectable({ providedIn: 'root' })
export class PrimeNavRequestService {
  private readonly _pendingPath = signal<string | null>(null);
  private readonly _requestedPeriod = signal<string | null>(null);

  readonly pendingPath = this._pendingPath.asReadonly();
  readonly requestedPeriod = this._requestedPeriod.asReadonly();

  requestView(path: string): void {
    this._pendingPath.set(path);
  }

  /** Navigation + transmission d'une période pré-sélectionnée à la page cible. */
  requestViewWithPeriod(path: string, period: string): void {
    this._requestedPeriod.set(period);
    this._pendingPath.set(path);
  }

  clearPending(): void {
    this._pendingPath.set(null);
  }

  clearRequestedPeriod(): void {
    this._requestedPeriod.set(null);
  }

  /** Annule toute navigation en attente (changement d’identité mode développeur). */
  clearAll(): void {
    this._pendingPath.set(null);
    this._requestedPeriod.set(null);
  }
}
