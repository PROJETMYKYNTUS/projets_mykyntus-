import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Ancienne barre de recherche par module Prime — supprimée au profit de la
 * recherche globale unique dans la topbar du shell. Ne rend plus rien.
 */
@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [],
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopbarComponent {}
