import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Inbox, Loader2 } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  INITIAL_TRAINING_STATUS_LABELS,
  TRAINING_ATTENDANCE_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type InitialTrainingPathDto,
  type MyAssignedTrainingSessionDto,
  type TrainingAttendance,
  type TrainingSessionStatus,
} from '../../../core/models/formation-training.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';

@Component({
  selector: 'app-formation-employee',
  standalone: true,
  imports: [CommonModule, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './formation-employee.component.html',
  styleUrls: ['./formation-employee.component.css'],
})
export class FormationEmployeeComponent implements OnInit {
  readonly icons = {
    inbox: Inbox,
    loader: Loader2,
  };

  initialPaths: InitialTrainingPathDto[] = [];
  assignedSessions: MyAssignedTrainingSessionDto[] = [];
  initialStatusLabels = INITIAL_TRAINING_STATUS_LABELS;
  sessionStatusLabels = TRAINING_SESSION_STATUS_LABELS;
  attendanceLabels = TRAINING_ATTENDANCE_LABELS;
  loading = false;

  userId = '';

  constructor(
    private trainingApi: FormationTrainingService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const user = JSON.parse(localStorage.getItem('user') || '{}');

    const rawId = user?.id;
    if (typeof rawId === 'string' && rawId.includes('-')) {
      this.userId = rawId;
    } else if (user?.guid && String(user.guid).includes('-')) {
      this.userId = String(user.guid);
    } else {
      const padded = String(rawId).padStart(12, '0');
      this.userId = `00000000-0000-0000-0000-${padded}`;
    }

    void this.reload();
  }

  get isEmpty(): boolean {
    return !this.loading && this.initialPaths.length === 0 && this.assignedSessions.length === 0;
  }

  private async reload(): Promise<void> {
    this.loading = true;
    this.cdr.detectChanges();
    await Promise.all([this.loadInitialPaths(), this.loadAssignedSessions()]);
    this.loading = false;
    this.cdr.detectChanges();
  }

  private async loadInitialPaths(): Promise<void> {
    if (!this.userId?.includes('-')) {
      this.initialPaths = [];
      return;
    }
    try {
      this.initialPaths = await this.trainingApi.listInitialByEmployee(this.userId);
    } catch {
      this.initialPaths = [];
    }
  }

  private async loadAssignedSessions(): Promise<void> {
    if (!this.userId?.includes('-')) {
      this.assignedSessions = [];
      return;
    }
    try {
      this.assignedSessions = await this.trainingApi.listMyAssignedSessions(this.userId);
    } catch {
      this.assignedSessions = [];
    }
  }

  sessionStatusLabel(status: TrainingSessionStatus | string): string {
    return this.sessionStatusLabels[status as TrainingSessionStatus] ?? String(status);
  }

  attendanceLabel(attendance: TrainingAttendance | string): string {
    const key = (attendance || 'Pending') as TrainingAttendance;
    return this.attendanceLabels[key] ?? String(attendance);
  }
}
