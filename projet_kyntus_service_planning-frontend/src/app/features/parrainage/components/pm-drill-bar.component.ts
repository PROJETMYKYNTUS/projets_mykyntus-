import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ParrainageRoleService } from '../state/parrainage-role.service';
import { HierarchyDrillService } from '../state/hierarchy-drill.service';
import { ORG_NODES, listCoachesUnderManager, listManagersUnderRp } from '../lib/org-hierarchy';

const LABELS: Record<string, string> = {
  'rp-1': 'RP',
  'mgr-1': 'Manager',
  'coach-1': 'Coach',
};

@Component({
  selector: 'app-pm-drill-bar',
  standalone: true,
  template: `
    @if (role.user().role === 'MANAGER') {
      <div class="flex flex-wrap items-center gap-2 mb-4">
        <span class="text-xs text-muted uppercase">Périmètre</span>
        <select
          [value]="drill.drill().coachId ?? ''"
          (change)="setCoach($event)"
          class="bg-slate-900 border border-slate-700 rounded-lg py-2 px-3 text-sm text-primary"
        >
          <option value="">Coach</option>
          @for (n of coaches; track n.id) {
            <option [value]="n.id">{{ label(n.id) }}</option>
          }
        </select>
      </div>
    } @else if (role.user().role === 'RP') {
      <div class="flex flex-wrap items-center gap-2 mb-4">
        <span class="text-xs text-muted uppercase">Périmètre</span>
        <select
          [value]="drill.drill().managerId ?? ''"
          (change)="setManager($event)"
          class="bg-slate-900 border border-slate-700 rounded-lg py-2 px-3 text-sm text-primary"
        >
          <option value="">Manager</option>
          @for (m of managers; track m.id) {
            <option [value]="m.id">{{ label(m.id) }}</option>
          }
        </select>
        <select
          [value]="drill.drill().coachId ?? ''"
          (change)="setCoach($event)"
          [disabled]="!drill.drill().managerId"
          class="bg-slate-900 border border-slate-700 rounded-lg py-2 px-3 text-sm text-primary disabled:opacity-50"
        >
          <option value="">Coach</option>
          @for (n of coaches; track n.id) {
            <option [value]="n.id">{{ label(n.id) }}</option>
          }
        </select>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PmDrillBarComponent {
  readonly role = inject(ParrainageRoleService);
  readonly drill = inject(HierarchyDrillService);

  get managers() {
    const u = this.role.user();
    return u.role === 'RP' ? listManagersUnderRp(ORG_NODES, u.id) : [];
  }

  get coaches() {
    const u = this.role.user();
    const mgrId = u.role === 'RP' ? this.drill.drill().managerId : u.role === 'MANAGER' ? u.id : undefined;
    return mgrId ? listCoachesUnderManager(ORG_NODES, mgrId) : [];
  }

  label(id: string): string {
    return LABELS[id] ?? id;
  }

  setManager(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.drill.setDrill({ managerId: id || undefined, coachId: undefined });
  }

  setCoach(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.drill.setDrill({ coachId: id || undefined });
  }
}
