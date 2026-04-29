import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

/**
 * Navigation vers l'espace Documentation intégré dans le frontend principal.
 */
@Injectable({ providedIn: 'root' })
export class DocumentationNavigationService {
  constructor(private readonly router: Router) {}

  /** Ouvre l’interface Documentation « RH » (shell RH). */
  openRhInPlanningShell(): void {
    void this.router.navigate(['/documentation'], { queryParams: { doc: 'rh' } });
  }

  /** Ouvre l’interface Documentation « pilote ». */
  openPilotInPlanningShell(): void {
    void this.router.navigate(['/documentation'], { queryParams: { doc: 'pilot' } });
  }
}
