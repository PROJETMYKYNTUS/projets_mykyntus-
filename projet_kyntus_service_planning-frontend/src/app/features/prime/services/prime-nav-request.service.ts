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

  /** Vue Prime active (synchronisée par le layout pour le menu global). */
  private readonly _activePath = signal<string>('/');
  readonly activePath = this._activePath.asReadonly();

  setActivePath(path: string): void {
    this._activePath.set(path);
  }

  requestView(path: string): void {
    this._pendingPath.set(path);
  }

  /** Navigation + transmission d'une période pré-sélectionnée à la page cible. */
  requestViewWithPeriod(path: string, period: string): void {
    this._requestedPeriod.set(period);
    this._pendingPath.set(path);
  }

  private readonly _requestedSynthesisScope = signal<{
    period: string;
    scopeType: string;
    scopeId: string;
  } | null>(null);

  readonly requestedSynthesisScope = this._requestedSynthesisScope.asReadonly();

  /** Navigation vers la synthèse globale avec périmètre pré-sélectionné. */
  requestViewWithSynthesisScope(
    path: string,
    scope: { period: string; scopeType: string; scopeId: string },
  ): void {
    this._requestedSynthesisScope.set(scope);
    this._pendingPath.set(path);
  }

  clearRequestedSynthesisScope(): void {
    this._requestedSynthesisScope.set(null);
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
    this._requestedSynthesisScope.set(null);
  }
}
