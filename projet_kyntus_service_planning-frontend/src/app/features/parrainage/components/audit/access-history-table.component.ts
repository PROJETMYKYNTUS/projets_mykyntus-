import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Search, Shield } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { SeverityBadgeComponent } from './audit-badges.component';
import { enrichAccessWithBruteForce, accessTypeLabel, AccessRowView } from '../../audit/audit-access-utils';
import type { AccessLogRow } from '../../audit/audit-types';

@Component({
  selector: 'app-access-history-table',
  standalone: true,
  imports: [LucideIconComponent, SeverityBadgeComponent],
  template: `
    <div class="space-y-4">
      <div class="rounded-lg border border-emerald-500/25 bg-emerald-500/5 px-4 py-3 text-xs text-primary">
        <span class="font-semibold text-emerald-300/90">Sécurité — </span>
        Uniquement connexions réussies ou échouées et déconnexions. Aucune action métier (création dossier, suppression, etc.).
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <div class="relative flex-1 min-w-[200px]">
          <app-lucide-icon [icon]="searchIcon" className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
          <input [value]="q()" (input)="q.set($any($event.target).value)" placeholder="Rechercher utilisateur, IP, lieu…"
            class="w-full bg-input border border-default rounded-lg pl-9 pr-3 py-2 text-sm text-primary transition-colors focus:border-emerald-500/40 focus:ring-1 focus:ring-emerald-500/20" />
        </div>
        @if (hasBrute()) {
          <span class="text-xs text-amber-300 flex items-center gap-1.5">
            <app-lucide-icon [icon]="shieldIcon" className="w-4 h-4 shrink-0" />
            Détection brute force (≥5 échecs / 2 min)
          </span>
        }
      </div>
      <div class="card-navy overflow-x-auto border border-default/80">
        <table class="w-full text-sm min-w-[920px]">
          <thead class="bg-card/55 text-muted text-left">
            <tr>
              <th class="px-4 py-3">Utilisateur</th>
              <th class="px-4 py-3">Date / heure</th>
              <th class="px-4 py-3">IP</th>
              <th class="px-4 py-3">Localisation</th>
              <th class="px-4 py-3">Statut</th>
              <th class="px-4 py-3">Sécurité</th>
              <th class="px-4 py-3">Type</th>
              <th class="px-4 py-3">Détail</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            @if (filtered().length === 0) {
              <tr>
                <td colspan="8" class="px-4 py-8 text-center text-sm text-muted">Aucun historique d'accès disponible.</td>
              </tr>
            }
            @for (r of filtered(); track r.id) {
              <tr class="hover:bg-input/35 transition-colors">
                <td class="px-4 py-3 text-primary font-medium">{{ r.user }}</td>
                <td class="px-4 py-3 text-muted whitespace-nowrap">{{ r.datetime }}</td>
                <td class="px-4 py-3 text-primary font-mono text-xs">{{ r.ip }}</td>
                <td class="px-4 py-3 text-muted">{{ r.location }}</td>
                <td class="px-4 py-3">
                  @if (r.success) {
                    <span class="text-emerald-400 text-xs font-medium">Succès</span>
                  } @else {
                    <span class="text-rose-400 text-xs font-medium">Échec</span>
                  }
                </td>
                <td class="px-4 py-3">
                  @if (r.bruteForce) {
                    <app-severity-badge level="WARNING" />
                  } @else {
                    <span class="text-[10px] text-muted">—</span>
                  }
                </td>
                <td class="px-4 py-3 text-primary">{{ typeLabel(r) }}</td>
                <td class="px-4 py-3 text-muted text-xs max-w-[240px]">{{ r.detail ?? '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessHistoryTableComponent {
  readonly searchIcon = Search;
  readonly shieldIcon = Shield;

  readonly q = signal('');
  private readonly rows = enrichAccessWithBruteForce([] as AccessLogRow[]);

  readonly filtered = computed(() => {
    const qq = this.q().trim().toLowerCase();
    return this.rows.filter(
      (r) => !qq || `${r.user} ${r.ip} ${r.location} ${r.label} ${r.detail ?? ''}`.toLowerCase().includes(qq),
    );
  });

  readonly hasBrute = computed(() => this.filtered().some((r) => r.bruteForce));

  typeLabel(r: AccessRowView): string {
    return accessTypeLabel(r);
  }
}
