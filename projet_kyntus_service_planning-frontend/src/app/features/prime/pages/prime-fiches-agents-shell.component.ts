import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  effect,
  inject,
  signal,
} from '@angular/core';
import { PrimeFichesPilotesPageComponent } from './prime-fiches-pilotes-page.component';
import { PrimeFicheImportComponent } from './prime-fiche-import.component';
import { PrimeNavRequestService } from '../services/prime-nav-request.service';

type AgentsTab = 'pilotage' | 'import';

@Component({
  selector: 'app-prime-fiches-agents-shell',
  standalone: true,
  imports: [PrimeFichesPilotesPageComponent, PrimeFicheImportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ky-page-shell">
      <header class="space-y-2">
        <h1 class="ky-page-title text-xl sm:text-2xl">Fiches PRIME agents</h1>
        <p class="ky-page-subtitle max-w-3xl">
          Pilotage des fiches par pilote ou import d'une fiche Excel/CSV déjà remplie.
        </p>
      </header>

      <div class="flex flex-wrap gap-2 border-b border-default pb-4">
        <button type="button" (click)="tab.set('pilotage')" [class]="tabClass('pilotage')">
          Pilotage fiches agents
        </button>
        <button type="button" (click)="tab.set('import')" [class]="tabClass('import')">
          Import fiche prête
        </button>
      </div>

      @if (tab() === 'pilotage') {
        <app-prime-fiches-pilotes-page [embeddedInShell]="true" />
      } @else {
        <app-prime-fiche-import [embeddedInShell]="true" />
      }
    </div>
  `,
})
export class PrimeFichesAgentsShellComponent implements OnInit {
  private readonly nav = inject(PrimeNavRequestService);
  readonly tab = signal<AgentsTab>('pilotage');

  constructor() {
    effect(() => {
      const requested = this.nav.requestedTab();
      if (!requested) return;
      const t = requested.trim().toLowerCase();
      if (t === 'import' || t === 'pilotage') {
        this.tab.set(t);
        this.nav.clearRequestedTab();
      }
    });
  }

  ngOnInit(): void {
    const requested = this.nav.requestedTab();
    if (requested) {
      const t = requested.trim().toLowerCase();
      if (t === 'import' || t === 'pilotage') this.tab.set(t);
      this.nav.clearRequestedTab();
    }
  }

  tabClass(t: AgentsTab): string {
    const active = this.tab() === t;
    return active
      ? 'rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white'
      : 'rounded-lg border border-default bg-card px-4 py-2 text-sm font-medium text-primary hover:bg-input/40';
  }
}
