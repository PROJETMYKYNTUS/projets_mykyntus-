import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ArrowLeft, History } from 'lucide';
import { resolveCurrentUserGuid } from '../../../core/lib/user-guid.util';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { MyQuizAttemptHistoryItemDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

@Component({
  selector: 'app-formation-quiz-history',
  standalone: true,
  imports: [CommonModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-quiz-history.component.html',
  styleUrls: ['./formation-quiz-history.component.css'],
})
export class FormationQuizHistoryComponent implements OnInit {
  readonly icons = { back: ArrowLeft, history: History };
  private readonly api = inject(FormationTrainingService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly rows = signal<MyQuizAttemptHistoryItemDto[]>([]);

  ngOnInit(): void {
    void this.reload();
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const userId = resolveCurrentUserGuid();
      if (!userId?.includes('-')) {
        this.error.set('Utilisateur non identifié.');
        this.rows.set([]);
        return;
      }
      this.rows.set(await this.api.listMyQuizHistory(userId));
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
      this.rows.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
