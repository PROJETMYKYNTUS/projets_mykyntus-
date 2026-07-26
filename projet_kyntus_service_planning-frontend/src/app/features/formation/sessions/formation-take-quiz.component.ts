import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ArrowLeft, CheckCircle2 } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type {
  MyAssignedTrainingSessionDto,
  TrainingQuizAttemptDto,
  TrainingQuizForEmployeeDto,
} from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

type Step = 'questions' | 'recap' | 'done';

@Component({
  selector: 'app-formation-take-quiz',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-take-quiz.component.html',
  styleUrls: ['./formation-take-quiz.component.css'],
})
export class FormationTakeQuizComponent implements OnInit {
  readonly icons = { back: ArrowLeft, done: CheckCircle2 };

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(FormationTrainingService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  sessionId = '';
  userId = '';
  assigned: MyAssignedTrainingSessionDto | null = null;
  quiz: TrainingQuizForEmployeeDto | null = null;
  attempt: TrainingQuizAttemptDto | null = null;
  step: Step = 'questions';
  currentIndex = 0;
  answers: Record<string, { selectedOptionIndex?: number; selectedOptionIndexes?: number[]; freeText?: string }> = {};

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    this.userId = this.resolveUserId();
    if (!this.sessionId || !this.userId) {
      void this.router.navigate(['/mes-formations']);
      return;
    }
    void this.load();
  }

  get totalQuestions(): number {
    return this.quiz?.questions.length ?? 0;
  }

  get currentQuestion() {
    return this.quiz?.questions[this.currentIndex] ?? null;
  }

  get progressPercent(): number {
    if (!this.totalQuestions) return 0;
    if (this.step === 'recap' || this.step === 'done') return 100;
    return Math.round(((this.currentIndex + 1) / this.totalQuestions) * 100);
  }

  isQcm(type: string | number): boolean {
    return type === 'Qcm' || type === 0;
  }

  isMulti(q: { allowMultiple?: boolean }): boolean {
    return !!q.allowMultiple;
  }

  isSelectedMulti(questionId: string, index: number): boolean {
    return (this.answers[questionId]?.selectedOptionIndexes ?? []).includes(index);
  }

  toggleMulti(questionId: string, index: number, checked: boolean): void {
    const a = this.answers[questionId] ?? {};
    const set = new Set(a.selectedOptionIndexes ?? []);
    if (checked) set.add(index);
    else set.delete(index);
    a.selectedOptionIndexes = [...set].sort((x, y) => x - y);
    a.selectedOptionIndex = a.selectedOptionIndexes[0];
    this.answers[questionId] = a;
  }

  canGoNext(): boolean {
    const q = this.currentQuestion;
    if (!q) return false;
    const a = this.answers[q.id];
    if (!a) return false;
    if (this.isQcm(q.type)) {
      if (this.isMulti(q)) return (a.selectedOptionIndexes?.length ?? 0) > 0;
      return a.selectedOptionIndex != null;
    }
    return !!(a.freeText && a.freeText.trim());
  }

  answerSummary(q: {
    id: string;
    type: string | number;
    options?: string[] | null;
    allowMultiple?: boolean;
  }): string {
    const a = this.answers[q.id];
    if (!a) return 'Non répondu';
    if (!this.isQcm(q.type)) return a.freeText?.trim() || 'Non répondu';
    const idxs = this.isMulti(q)
      ? a.selectedOptionIndexes ?? []
      : a.selectedOptionIndex != null
        ? [a.selectedOptionIndex]
        : [];
    if (!idxs.length) return 'Non répondu';
    return idxs.map((i) => q.options?.[i] || `Option ${i + 1}`).join(' · ');
  }

  prev(): void {
    if (this.step === 'recap') {
      this.step = 'questions';
      this.currentIndex = Math.max(0, this.totalQuestions - 1);
      return;
    }
    if (this.currentIndex > 0) this.currentIndex -= 1;
  }

  next(): void {
    if (this.step !== 'questions') return;
    if (!this.canGoNext()) {
      this.error.set('Répondez à la question avant de continuer.');
      return;
    }
    this.error.set(null);
    if (this.currentIndex < this.totalQuestions - 1) {
      this.currentIndex += 1;
      return;
    }
    this.step = 'recap';
  }

  goToQuestion(index: number): void {
    this.step = 'questions';
    this.currentIndex = index;
  }

  async submit(): Promise<void> {
    if (!this.quiz || !this.assigned) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      this.attempt = await this.api.submitQuizAttempt(this.sessionId, {
        assignmentId: this.assigned.assignmentId,
        employeeId: this.userId,
        answers: Object.entries(this.answers).map(([questionId, a]) => ({
          questionId,
          selectedOptionIndex: a.selectedOptionIndex ?? null,
          selectedOptionIndexes: a.selectedOptionIndexes?.length ? a.selectedOptionIndexes : null,
          freeText: a.freeText ?? null,
        })),
      });
      this.step = 'done';
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Échec soumission');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const list = await this.api.listMyAssignedSessions(this.userId);
      this.assigned = list.find((s) => s.sessionId === this.sessionId) ?? null;
      if (!this.assigned) {
        this.error.set('Session introuvable dans vos affectations.');
        return;
      }
      if (this.assigned.attemptId) {
        this.step = 'done';
        this.attempt = {
          id: this.assigned.attemptId,
          quizId: this.assigned.quizId ?? '',
          assignmentId: this.assigned.assignmentId,
          employeeId: this.userId,
          employeeName: '',
          finalScore: this.assigned.finalScore,
          passed: this.assigned.passed,
          isGraded: !!this.assigned.attemptGraded,
          submittedAt: '',
        };
        return;
      }
      if (!this.assigned.canTakeQuiz) {
        this.error.set('Ce quiz n’est pas disponible (présence requise, ou déjà soumis / non publié).');
        return;
      }
      this.quiz = await this.api.getQuizForEmployee(this.sessionId, this.userId);
      this.answers = {};
      for (const q of this.quiz.questions) {
        this.answers[q.id] = {};
      }
      this.step = 'questions';
      this.currentIndex = 0;
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  private resolveUserId(): string {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    const rawId = user?.id;
    if (typeof rawId === 'string' && rawId.includes('-')) return rawId;
    if (user?.guid && String(user.guid).includes('-')) return String(user.guid);
    const padded = String(rawId ?? '').padStart(12, '0');
    return `00000000-0000-0000-0000-${padded}`;
  }
}
