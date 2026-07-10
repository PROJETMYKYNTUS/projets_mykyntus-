import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { TrainingSessionDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';

@Component({
  selector: 'app-formation-rh-plan',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  templateUrl: './formation-rh-plan.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationRhPlanComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  readonly sessions = signal<TrainingSessionDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  form = {
    title: '',
    description: '',
    capacity: 10,
    plannedStart: '',
    plannedEnd: '',
    animatorKind: 'Internal' as 'Internal' | 'External',
    animatorUserId: '',
    externalAnimatorName: '',
    externalAnimatorOrganization: '',
    externalAnimatorEmail: '',
    externalAnimatorPhone: '',
  };

  ngOnInit(): void {
    void this.reload();
  }

  private async reload(): Promise<void> {
    try {
      this.sessions.set(await this.api.listSessions());
    } catch {
      this.sessions.set([]);
    }
  }

  async publish(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.createSession({
        title: this.form.title,
        description: this.form.description,
        capacity: this.form.capacity,
        plannedStart: this.form.plannedStart,
        plannedEnd: this.form.plannedEnd,
        animatorKind: this.form.animatorKind,
        animatorUserId: this.form.animatorKind === 'Internal' ? this.form.animatorUserId : null,
        externalAnimatorName: this.form.externalAnimatorName,
        externalAnimatorOrganization: this.form.externalAnimatorOrganization,
        externalAnimatorEmail: this.form.externalAnimatorEmail,
        externalAnimatorPhone: this.form.externalAnimatorPhone,
        createdByUserId: 'rh-ui',
        publish: true,
      });
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Échec de la création');
    } finally {
      this.busy.set(false);
    }
  }
}
