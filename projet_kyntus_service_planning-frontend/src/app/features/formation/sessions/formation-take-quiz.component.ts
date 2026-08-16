import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ArrowLeft, CheckCircle2 } from 'lucide';
import { resolveCurrentUserGuid } from '../../../core/lib/user-guid.util';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type {
  CatalogPlayerDto,
  MyAssignedTrainingSessionDto,
  TrainingQuizAttemptDto,
  TrainingQuizForEmployeeDto,
} from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { isQuizMediaVideo } from '../shared/formation-quiz-draft.types';

type Step = 'questions' | 'recap' | 'done' | 'review';

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
  catalogItemId = '';
  userId = '';
  enrollmentId = '';
  assigned: MyAssignedTrainingSessionDto | null = null;
  player: CatalogPlayerDto | null = null;
  quiz: TrainingQuizForEmployeeDto | null = null;
  attempt: TrainingQuizAttemptDto | null = null;
  history: TrainingQuizAttemptDto[] = [];
  step: Step = 'questions';
  currentIndex = 0;
  answers: Record<string, { selectedOptionIndex?: number; selectedOptionIndexes?: number[]; freeText?: string }> = {};

  get isSelfService(): boolean {
    return !!this.catalogItemId && !this.sessionId;
  }

  get backLink(): string[] {
    if (this.isSelfService) return ['/mes-formations', 'contenu', this.catalogItemId];
    if (this.sessionId) return ['/mes-formations', this.sessionId, 'contenu'];
    return ['/mes-formations'];
  }

  get pageTitle(): string {
    return this.quiz?.title || this.assigned?.title || this.player?.title || 'Passer le quiz';
  }

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    this.catalogItemId = this.route.snapshot.paramMap.get('catalogItemId') ?? '';
    this.userId = resolveCurrentUserGuid();
    if ((!this.sessionId && !this.catalogItemId) || !this.userId) {
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

  isMediaVideo(url: string | null | undefined, mediaKind?: string | null): boolean {
    return isQuizMediaVideo(url, mediaKind);
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

  selectedLabels(ans: {
    options?: string[] | null;
    selectedOptionIndexes?: number[] | null;
    selectedOptionIndex?: number | null;
  }): string {
    const idxs =
      ans.selectedOptionIndexes?.length
        ? ans.selectedOptionIndexes
        : ans.selectedOptionIndex != null
          ? [ans.selectedOptionIndex]
          : [];
    if (!idxs.length) return '—';
    return idxs.map((i) => ans.options?.[i] || `Option ${i + 1}`).join(' · ');
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
    if (!this.quiz) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const answers = Object.entries(this.answers).map(([questionId, a]) => ({
        questionId,
        selectedOptionIndex: a.selectedOptionIndex ?? null,
        selectedOptionIndexes: a.selectedOptionIndexes?.length ? a.selectedOptionIndexes : null,
        freeText: a.freeText ?? null,
      }));

      if (this.isSelfService) {
        this.attempt = await this.api.submitCatalogQuizAttempt(this.catalogItemId, {
          assignmentId: this.enrollmentId || this.quiz.enrollmentId || '',
          employeeId: this.userId,
          answers,
        });
        this.history = await this.api.listMyCatalogQuizAttempts(this.catalogItemId);
      } else {
        if (!this.assigned) return;
        this.attempt = await this.api.submitQuizAttempt(this.sessionId, {
          assignmentId: this.assigned.assignmentId,
          employeeId: this.userId,
          answers,
        });
        this.history = await this.api.listMyQuizAttempts(this.sessionId, this.userId);
      }
      this.step = this.attempt.answers?.length ? 'review' : 'done';
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Échec soumission');
    } finally {
      this.busy.set(false);
    }
  }

  async restart(): Promise<void> {
    if (!this.quiz?.allowMultipleAttempts && !this.assigned?.allowMultipleAttempts) return;
    this.attempt = null;
    this.step = 'questions';
    this.currentIndex = 0;
    this.answers = {};
    this.quiz = this.isSelfService
      ? await this.api.getCatalogQuizForEmployee(this.catalogItemId)
      : await this.api.getQuizForEmployee(this.sessionId, this.userId);
    for (const q of this.quiz.questions) this.answers[q.id] = {};
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      if (this.isSelfService) {
        await this.loadSelfService();
      } else {
        await this.loadSession();
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadSelfService(): Promise<void> {
    this.player = await this.api.getCatalogPlayerByCatalog(this.catalogItemId);
    this.enrollmentId = this.player.enrollmentId;
    this.history = await this.api.listMyCatalogQuizAttempts(this.catalogItemId).catch(() => []);

    if (!this.player.canTakeQuiz) {
      if (this.history.length) {
        this.attempt = this.history[0];
        this.step = this.attempt.answers?.length ? 'review' : 'done';
        return;
      }
      this.error.set(
        this.player.quizBlockedReason ||
          'Ce quiz n’est pas disponible (contenu requis, ou déjà soumis).',
      );
      return;
    }

    this.quiz = await this.api.getCatalogQuizForEmployee(this.catalogItemId);
    this.enrollmentId = this.quiz.enrollmentId || this.enrollmentId;
    this.answers = {};
    for (const q of this.quiz.questions) {
      this.answers[q.id] = {};
    }
    this.step = 'questions';
    this.currentIndex = 0;
  }

  private async loadSession(): Promise<void> {
    const list = await this.api.listMyAssignedSessions();
    this.assigned = list.find((s) => s.sessionId === this.sessionId) ?? null;
    if (!this.assigned) {
      this.error.set('Session introuvable dans vos affectations.');
      return;
    }
    this.history = await this.api.listMyQuizAttempts(this.sessionId, this.userId).catch(() => []);
    if (this.assigned.attemptId && !this.assigned.canTakeQuiz) {
      this.attempt = this.history[0] ?? {
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
      this.step = this.attempt.answers?.length ? 'review' : 'done';
      return;
    }
    if (!this.assigned.canTakeQuiz) {
      this.error.set(
        this.assigned.quizBlockedReason ||
          'Ce quiz n’est pas disponible (présence/contenu requis, ou déjà soumis / non publié).',
      );
      return;
    }
    this.quiz = await this.api.getQuizForEmployee(this.sessionId, this.userId);
    this.answers = {};
    for (const q of this.quiz.questions) {
      this.answers[q.id] = {};
    }
    this.step = 'questions';
    this.currentIndex = 0;
  }
}
