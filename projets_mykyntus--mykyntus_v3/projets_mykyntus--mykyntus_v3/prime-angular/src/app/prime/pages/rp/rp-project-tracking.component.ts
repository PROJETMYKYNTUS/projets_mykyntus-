import {
  ChangeDetectionStrategy,
  Component,
  Input,
  inject,
  signal,
  effect,
} from '@angular/core';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { RpPrimeService } from '../../services/rp-prime.service';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';

@Component({
  selector: 'app-rp-project-tracking',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    <div class="space-y-6">
      <div>
        <h2 class="text-2xl font-bold text-white tracking-tight">Avancement remplissage PRIME</h2>
        <p class="text-slate-400 mt-1">Indicateur de complétion des fiches et jalons de saisie (hors tâches externes).</p>
      </div>

      <app-prime-card title="Progression par lot / période">
        <div class="space-y-4">
          @for (project of projectData(); track project.projectName) {
            <div class="p-4 rounded-xl bg-navy-900 border border-default">
              <div class="flex justify-between items-center mb-2">
                <span class="text-slate-200 font-medium">{{ project.projectName }}</span>
                <span class="text-cyan-300 font-semibold">{{ progressPct(project) }}%</span>
              </div>
              <div class="h-2 rounded-full bg-navy-800 overflow-hidden">
                <div class="h-full bg-cyan-500" [style.width.%]="progressPct(project)"></div>
              </div>
              <p class="text-xs text-slate-400 mt-2">
                {{ project.completedTasks }} étapes complétées sur {{ project.totalTasks }}
              </p>
            </div>
          }
        </div>
      </app-prime-card>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RpProjectTrackingComponent {
  @Input({ required: true }) rpUserId!: string;
  readonly hierarchy = inject(HierarchyDrillService);

  readonly projectData = signal<
    Array<{ projectName: string; completedTasks: number; totalTasks: number }>
  >([]);

  constructor() {
    effect(() => {
      const id = this.rpUserId;
      const drill = this.hierarchy.drill();
      void drill.managerId;
      void drill.coachId;
      void id;
      RpPrimeService.getTeamPerformanceByProject(this.rpUserId, this.hierarchy.drill()).then((rows) => {
        const grouped = rows.reduce<
          Record<string, { projectName: string; completedTasks: number; totalTasks: number }>
        >((acc, row) => {
          if (!acc[row.projectId]) {
            acc[row.projectId] = {
              projectName: row.projectName,
              completedTasks: 0,
              totalTasks: 0,
            };
          }
          acc[row.projectId].completedTasks += row.completedTasks;
          acc[row.projectId].totalTasks += row.totalTasks;
          return acc;
        }, {});
        this.projectData.set(Object.values(grouped));
      });
    });
  }

  progressPct(project: { completedTasks: number; totalTasks: number }): number {
    return Math.round((project.completedTasks / Math.max(project.totalTasks, 1)) * 100);
  }
}
