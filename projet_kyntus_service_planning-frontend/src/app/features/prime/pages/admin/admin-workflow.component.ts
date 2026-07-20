import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { GitBranch } from 'lucide';
import { AdminPrimeService } from '../../services/admin-prime.service';
import type { AdminWorkflowConfig } from '../../models/admin.models';
import { WorkflowAdminComponent } from '../../components/admin/workflow-admin.component';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

@Component({
  selector: 'app-admin-workflow',
  standalone: true,
  imports: [WorkflowAdminComponent, LucideIconComponent],
  template: `
    @if (!workflow()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-cyan-500"></div>
      </div>
    } @else {
      <div class="prime-page-shell">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 bg-blue-600/10 rounded-xl flex items-center justify-center text-blue-500">
            <app-lucide-icon [icon]="icons.branch" className="w-6 h-6" />
          </div>
          <div>
            <h3 class="text-xl font-bold text-primary">Configuration du workflow</h3>
            <p class="text-sm text-muted">Pilote → Coach → Manager → RP → RH</p>
          </div>
        </div>
        <app-workflow-admin
          [workflow]="workflow()!"
          [saving]="saving()"
          (workflowChange)="onWorkflowChange($event)"
          (save)="save()"
        />
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminWorkflowComponent implements OnInit {
  readonly workflow = signal<AdminWorkflowConfig | null>(null);
  readonly saving = signal(false);
  readonly icons = { branch: GitBranch };

  ngOnInit(): void {
    AdminPrimeService.getWorkflowConfig().then((w) => this.workflow.set(w));
  }

  onWorkflowChange(next: AdminWorkflowConfig): void {
    this.workflow.set(next);
  }

  async save(): Promise<void> {
    const w = this.workflow();
    if (!w) return;
    this.saving.set(true);
    try {
      const saved = await AdminPrimeService.saveWorkflowConfig(w);
      this.workflow.set(saved);
    } finally {
      this.saving.set(false);
    }
  }
}
