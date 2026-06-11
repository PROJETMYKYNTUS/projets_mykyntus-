import { ChangeDetectionStrategy, Component, Input, Output, EventEmitter } from '@angular/core';
import { Search, Users } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import type { Referral } from '../models/referral.model';

export interface TeamMember {
  id: string;
  name: string;
  role: string;
  projectName: string;
  referralCount: number;
  successCount: number;
}

@Component({
  selector: 'app-team-table',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    @if (loading) {
      <div class="card-navy p-10 text-center text-slate-500 text-sm">Chargement…</div>
    } @else if (members.length === 0) {
      <div class="card-navy p-12 text-center">
        <div class="w-16 h-16 bg-navy-800 rounded-full flex items-center justify-center mx-auto mb-4">
          <app-lucide-icon [icon]="usersIcon" className="w-8 h-8 text-slate-600" />
        </div>
        <h4 class="text-slate-300 font-medium">Aucun membre</h4>
        <p class="text-slate-500 text-sm mt-1">Aucune donnée à afficher</p>
      </div>
    } @else {
      <div class="space-y-6">
        @if (searchable) {
          <div class="relative flex-1 max-w-md">
            <app-lucide-icon [icon]="searchIcon" className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
            <input
              type="search"
              placeholder="Rechercher…"
              (input)="search.emit($any($event.target).value)"
              class="w-full bg-navy-900 border border-navy-800 rounded-lg py-2 pl-10 pr-4 text-sm text-slate-300 focus:outline-none focus:border-blue-500 transition-all"
            />
          </div>
        }
        <div class="card-navy overflow-hidden">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="bg-navy-800/50 border-b border-navy-800">
                <th class="px-6 py-4 text-[11px] font-bold text-slate-500 uppercase tracking-wider">Nom</th>
                <th class="px-6 py-4 text-[11px] font-bold text-slate-500 uppercase tracking-wider">Rôle</th>
                <th class="px-6 py-4 text-[11px] font-bold text-slate-500 uppercase tracking-wider">Projet</th>
                <th class="px-6 py-4 text-[11px] font-bold text-slate-500 uppercase tracking-wider">Parrainages</th>
                <th class="px-6 py-4 text-[11px] font-bold text-slate-500 uppercase tracking-wider">Taux de succès</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-navy-800">
              @for (m of members; track m.id) {
                <tr class="hover:bg-navy-800/30 transition-colors">
                  <td class="px-6 py-4 text-sm font-medium text-slate-200">{{ m.name }}</td>
                  <td class="px-6 py-4 text-sm text-slate-400">{{ m.role }}</td>
                  <td class="px-6 py-4 text-sm text-slate-400">{{ m.projectName }}</td>
                  <td class="px-6 py-4 text-sm text-slate-300">{{ m.referralCount }}</td>
                  <td class="px-6 py-4">
                    <span [class]="'text-sm font-medium ' + (successRate(m) >= 50 ? 'text-emerald-500' : 'text-slate-400')">{{ successRate(m) }}%</span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamTableComponent {
  @Input({ required: true }) members: TeamMember[] = [];
  @Input() loading = false;
  @Input() searchable = false;
  @Output() search = new EventEmitter<string>();

  readonly searchIcon = Search;
  readonly usersIcon = Users;

  successRate(m: TeamMember): number {
    return m.referralCount > 0 ? Math.round((m.successCount / m.referralCount) * 100) : 0;
  }
}

export function buildTeamMembersFromReferrals(referrals: Referral[]): TeamMember[] {
  const byReferrer = new Map<string, { name: string; projectName: string; total: number; success: number }>();
  for (const r of referrals) {
    const success = r.status === 'APPROVED' || r.status === 'REWARDED' ? 1 : 0;
    const cur = byReferrer.get(r.referrerId);
    if (cur) {
      cur.total++;
      cur.success += success;
    } else {
      byReferrer.set(r.referrerId, { name: r.referrerName, projectName: r.projectName, total: 1, success });
    }
  }
  return Array.from(byReferrer.entries()).map(([id, d]) => ({
    id,
    name: d.name,
    role: 'Collaborateur',
    projectName: d.projectName,
    referralCount: d.total,
    successCount: d.success,
  }));
}
