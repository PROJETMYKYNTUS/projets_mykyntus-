import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  Plus,
  Trash2,
} from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  TRAINING_QUIZ_STATUS_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type TrainingQuizAttemptDto,
  type TrainingQuizDto,
  type TrainingQuizStatus,
  type TrainingSessionDto,
} from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

type QuizDraftQuestion = {
  type: 'Qcm' | 'FreeText';
  prompt: string;
  options: string[];
  correctOptionIndex: number;
  correctOptionIndexes: number[];
  allowMultiple: boolean;
  points: number;
};

type QuizTab = 'edit' | 'results';

@Component({
  selector: 'app-formation-session-quiz',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-session-quiz.component.html',
  styleUrls: ['./formation-session-quiz.component.css'],
})
export class FormationSessionQuizComponent implements OnInit {
  readonly icons = {
    back: ArrowLeft,
    add: Plus,
    remove: Trash2,
    up: ArrowUp,
    down: ArrowDown,
  };

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly sessionDto = signal<TrainingSessionDto | null>(null);
  readonly quiz = signal<TrainingQuizDto | null>(null);
  readonly attempts = signal<TrainingQuizAttemptDto[]>([]);
  readonly tab = signal<QuizTab>('edit');

  sessionId = '';
  quizTitle = '';
  passThreshold = 70;
  questions: QuizDraftQuestion[] = [];
  rejectReason = '';

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    if (!this.sessionId) {
      void this.router.navigate(['/mes-sessions']);
      return;
    }
    void this.reload();
  }

  quizStatusLabel(status: TrainingQuizStatus | string | number | null | undefined): string {
    if (status == null) return 'Aucun quiz';
    if (typeof status === 'number') {
      const map = ['Draft', 'Published', 'Graded', 'Validated', 'Rejected'] as const;
      const key = map[status];
      return key ? TRAINING_QUIZ_STATUS_LABELS[key] : String(status);
    }
    return TRAINING_QUIZ_STATUS_LABELS[status as TrainingQuizStatus] ?? String(status);
  }

  sessionStatusLabel(status: string): string {
    return TRAINING_SESSION_STATUS_LABELS[status as keyof typeof TRAINING_SESSION_STATUS_LABELS] ?? status;
  }

  setTab(tab: QuizTab): void {
    this.tab.set(tab);
    if (tab === 'results') void this.loadAttempts();
  }

  isLocked(): boolean {
    const status = this.normalizeStatus(this.quiz()?.status);
    return status === 'Validated';
  }

  canPublish(): boolean {
    const status = this.normalizeStatus(this.quiz()?.status);
    return !!this.quiz() && (status === 'Draft' || status === 'Rejected');
  }

  canValidate(): boolean {
    const status = this.normalizeStatus(this.quiz()?.status);
    return status === 'Published' || status === 'Graded';
  }

  readonly expandedAttemptId = signal<string | null>(null);

  resultsSummary(): { submitted: number; graded: number; passed: number; rate: number } {
    const list = this.attempts();
    const graded = list.filter((a) => a.isGraded);
    const passed = graded.filter((a) => a.passed === true);
    return {
      submitted: list.length,
      graded: graded.length,
      passed: passed.length,
      rate: graded.length ? Math.round((passed.length / graded.length) * 100) : 0,
    };
  }

  addQuestion(): void {
    this.questions = [
      ...this.questions,
      {
        type: 'Qcm',
        prompt: '',
        options: ['', ''],
        correctOptionIndex: 0,
        correctOptionIndexes: [0],
        allowMultiple: false,
        points: 1,
      },
    ];
  }

  removeQuestion(index: number): void {
    if (this.questions.length <= 1) return;
    this.questions = this.questions.filter((_, i) => i !== index);
  }

  onTypeChange(q: QuizDraftQuestion, type: 'Qcm' | 'FreeText'): void {
    q.type = type;
    if (type === 'Qcm' && q.options.length < 2) {
      q.options = ['', ''];
      q.correctOptionIndex = 0;
      q.correctOptionIndexes = [0];
      q.allowMultiple = false;
    }
  }

  toggleAllowMultiple(q: QuizDraftQuestion, allow: boolean): void {
    q.allowMultiple = allow;
    if (allow) {
      q.correctOptionIndexes = [q.correctOptionIndex];
    } else {
      q.correctOptionIndex = q.correctOptionIndexes[0] ?? 0;
      q.correctOptionIndexes = [q.correctOptionIndex];
    }
  }

  isCorrectIndex(q: QuizDraftQuestion, oi: number): boolean {
    return q.allowMultiple ? q.correctOptionIndexes.includes(oi) : q.correctOptionIndex === oi;
  }

  setCorrectSingle(q: QuizDraftQuestion, oi: number): void {
    q.correctOptionIndex = oi;
    q.correctOptionIndexes = [oi];
  }

  toggleCorrectMulti(q: QuizDraftQuestion, oi: number, checked: boolean): void {
    const set = new Set(q.correctOptionIndexes);
    if (checked) set.add(oi);
    else set.delete(oi);
    q.correctOptionIndexes = [...set].sort((a, b) => a - b);
    if (q.correctOptionIndexes.length === 0) q.correctOptionIndexes = [oi];
    q.correctOptionIndex = q.correctOptionIndexes[0];
  }

  toggleAttemptDetails(attemptId: string): void {
    this.expandedAttemptId.set(this.expandedAttemptId() === attemptId ? null : attemptId);
  }

  isQcmAnswer(type: string | number): boolean {
    return type === 'Qcm' || type === 0;
  }

  isSelected(
    ans: { selectedOptionIndexes?: number[] | null; selectedOptionIndex?: number | null },
    oi: number,
  ): boolean {
    if (ans.selectedOptionIndexes?.length) return ans.selectedOptionIndexes.includes(oi);
    return ans.selectedOptionIndex === oi;
  }

  isCorrectOption(
    ans: { correctOptionIndexes?: number[] | null; correctOptionIndex?: number | null },
    oi: number,
  ): boolean {
    if (ans.correctOptionIndexes?.length) return ans.correctOptionIndexes.includes(oi);
    return ans.correctOptionIndex === oi;
  }

  optionLabel(options: string[] | null | undefined, index: number | null | undefined): string {
    if (index == null || !options || index < 0 || index >= options.length) return '—';
    return options[index] || `Option ${index + 1}`;
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
    if (!idxs.length) return 'Aucune réponse';
    return idxs.map((i) => this.optionLabel(ans.options, i)).join(' · ');
  }

  correctLabels(ans: {
    options?: string[] | null;
    correctOptionIndexes?: number[] | null;
    correctOptionIndex?: number | null;
  }): string {
    const idxs =
      ans.correctOptionIndexes?.length
        ? ans.correctOptionIndexes
        : ans.correctOptionIndex != null
          ? [ans.correctOptionIndex]
          : [];
    if (!idxs.length) return '—';
    return idxs.map((i) => this.optionLabel(ans.options, i)).join(' · ');
  }

  moveQuestion(index: number, delta: number): void {
    const target = index + delta;
    if (target < 0 || target >= this.questions.length) return;
    const copy = [...this.questions];
    const [item] = copy.splice(index, 1);
    copy.splice(target, 0, item);
    this.questions = copy;
  }

  addOption(q: QuizDraftQuestion): void {
    q.options = [...q.options, ''];
  }

  removeOption(q: QuizDraftQuestion, optIndex: number): void {
    if (q.options.length <= 2) return;
    q.options = q.options.filter((_, i) => i !== optIndex);
    q.correctOptionIndexes = q.correctOptionIndexes
      .filter((i) => i !== optIndex)
      .map((i) => (i > optIndex ? i - 1 : i));
    if (q.correctOptionIndexes.length === 0) q.correctOptionIndexes = [0];
    if (q.correctOptionIndex >= q.options.length || q.correctOptionIndex === optIndex) {
      q.correctOptionIndex = q.correctOptionIndexes[0];
    } else if (q.correctOptionIndex > optIndex) {
      q.correctOptionIndex -= 1;
    }
  }

  trackByIndex(index: number): number {
    return index;
  }

  async saveDraft(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId || this.isLocked()) return;
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      const questions = this.buildPayloadQuestions();
      if (!this.quizTitle.trim()) throw new Error('Le titre du quiz est obligatoire.');
      if (questions.length === 0) throw new Error('Ajoutez au moins une question.');
      const threshold = Math.min(100, Math.max(1, Number(this.passThreshold) || 70));
      this.passThreshold = threshold;
      const saved = await this.api.upsertQuiz(this.sessionId, {
        title: this.quizTitle.trim(),
        passThreshold: threshold,
        animatorUserId: animatorId,
        questions,
      });
      this.applyQuiz(saved);
      this.message.set('Brouillon enregistré.');
      await this.refreshSession();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur enregistrement');
    } finally {
      this.busy.set(false);
    }
  }

  async publish(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    await this.saveDraft();
    if (this.error()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const published = await this.api.publishQuiz(this.sessionId, animatorId);
      this.applyQuiz(published);
      this.message.set('Quiz publié aux présents.');
      await this.refreshSession();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur publication');
    } finally {
      this.busy.set(false);
    }
  }

  async validate(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const validated = await this.api.validateQuiz(this.sessionId, animatorId);
      this.applyQuiz(validated);
      this.message.set('Quiz validé.');
      await this.refreshSession();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur validation');
    } finally {
      this.busy.set(false);
    }
  }

  async reject(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    const reason = this.rejectReason.trim() || window.prompt('Motif du rejet ?')?.trim();
    if (!reason) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const rejected = await this.api.rejectQuiz(this.sessionId, animatorId, reason);
      this.applyQuiz(rejected);
      this.rejectReason = '';
      this.message.set('Quiz rejeté — vous pouvez le modifier.');
      await this.refreshSession();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur rejet');
    } finally {
      this.busy.set(false);
    }
  }

  async loadAttempts(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    try {
      const list = await this.api.listQuizAttempts(this.sessionId, animatorId);
      this.attempts.set(list);
      // Ouvre automatiquement le détail s’il n’y a qu’une tentative (cas fréquent).
      if (list.length === 1) {
        this.expandedAttemptId.set(list[0].id);
      } else if (this.expandedAttemptId() && !list.some((a) => a.id === this.expandedAttemptId())) {
        this.expandedAttemptId.set(null);
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur tentatives');
    }
  }

  async gradeFreeText(
    attempt: TrainingQuizAttemptDto,
    questionId: string,
    isCorrect: boolean,
  ): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.gradeFreeTextAnswer(this.sessionId, attempt.id, {
        animatorUserId: animatorId,
        questionId,
        isCorrect,
      });
      this.attempts.update((list) => list.map((a) => (a.id === updated.id ? updated : a)));
      this.expandedAttemptId.set(updated.id);
      this.message.set(
        updated.isGraded
          ? `Notation terminée — score ${updated.finalScore ?? '—'} % (${updated.passed ? 'Valide' : 'Non valide'}).`
          : `Réponse libre marquée ${isCorrect ? 'Correcte' : 'Fausse'} — score provisoire ${updated.finalScore ?? '—'} %.`,
      );
      await this.refreshSession();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Erreur notation');
    } finally {
      this.busy.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.refreshSession();
      const existing = await this.api.getQuiz(this.sessionId);
      this.applyQuiz(existing);
      if (!existing) {
        this.quizTitle = this.sessionDto()?.title ? `Quiz — ${this.sessionDto()!.title}` : '';
        this.questions = [
          {
            type: 'Qcm',
            prompt: '',
            options: ['', ''],
            correctOptionIndex: 0,
            correctOptionIndexes: [0],
            allowMultiple: false,
            points: 1,
          },
        ];
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshSession(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) {
      this.sessionDto.set(null);
      return;
    }
    const list = await this.api.listMyAnimatedSessions(animatorId);
    const found = list.find((s) => s.id === this.sessionId) ?? null;
    this.sessionDto.set(found);
    if (!found) {
      this.error.set('Séance introuvable ou non assignée à votre compte animateur.');
    }
  }

  private applyQuiz(quiz: TrainingQuizDto | null): void {
    this.quiz.set(quiz);
    if (!quiz) return;
    this.quizTitle = quiz.title;
    this.passThreshold = Number(quiz.passThreshold) > 0 ? Number(quiz.passThreshold) : 70;
    this.questions = quiz.questions
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((q) => {
        const indexes =
          q.correctOptionIndexes?.length
            ? [...q.correctOptionIndexes]
            : [q.correctOptionIndex ?? 0];
        return {
          type: this.normalizeQuestionType(q.type),
          prompt: q.prompt,
          options: q.options?.length ? [...q.options] : ['', ''],
          correctOptionIndex: indexes[0] ?? 0,
          correctOptionIndexes: indexes,
          allowMultiple: !!q.allowMultiple,
          points: q.points || 1,
        };
      });
    if (this.questions.length === 0) {
      this.questions = [
        {
          type: 'Qcm',
          prompt: '',
          options: ['', ''],
          correctOptionIndex: 0,
          correctOptionIndexes: [0],
          allowMultiple: false,
          points: 1,
        },
      ];
    }
  }

  private buildPayloadQuestions(): Array<{
    type: number;
    prompt: string;
    options: string[] | null;
    correctOptionIndex: number | null;
    correctOptionIndexes: number[] | null;
    allowMultiple: boolean;
    points: number;
  }> {
    return this.questions
      .map((q) => {
        const prompt = q.prompt.trim();
        if (!prompt) return null;
        if (q.type === 'FreeText') {
          return {
            type: 1,
            prompt,
            options: null,
            correctOptionIndex: null,
            correctOptionIndexes: null,
            allowMultiple: false,
            points: q.points || 1,
          };
        }
        const options = q.options.map((o) => o.trim()).filter(Boolean);
        if (options.length < 2) {
          throw new Error(`QCM « ${prompt} » : au moins 2 options requises.`);
        }
        const indexes = (q.allowMultiple ? q.correctOptionIndexes : [q.correctOptionIndex])
          .map((i) => Math.min(Math.max(0, i), options.length - 1))
          .filter((v, i, arr) => arr.indexOf(v) === i)
          .sort((a, b) => a - b);
        if (!indexes.length) {
          throw new Error(`QCM « ${prompt} » : indiquez au moins une bonne réponse.`);
        }
        return {
          type: 0,
          prompt,
          options,
          correctOptionIndex: indexes[0],
          correctOptionIndexes: indexes,
          allowMultiple: q.allowMultiple,
          points: q.points || 1,
        };
      })
      .filter((q): q is NonNullable<typeof q> => q != null);
  }

  private normalizeQuestionType(type: string | number): 'Qcm' | 'FreeText' {
    if (type === 'FreeText' || type === 1) return 'FreeText';
    return 'Qcm';
  }

  private normalizeStatus(status: TrainingQuizStatus | string | number | null | undefined): TrainingQuizStatus | null {
    if (status == null) return null;
    if (typeof status === 'number') {
      const map = ['Draft', 'Published', 'Graded', 'Validated', 'Rejected'] as const;
      return map[status] ?? null;
    }
    return status as TrainingQuizStatus;
  }

  private animatorId(): string | null {
    const stored = this.session.getStoredUser();
    const id =
      stored?.subjectId ||
      (JSON.parse(localStorage.getItem('user') || '{}')?.guid as string | undefined);
    if (id && String(id).includes('-')) return String(id);
    return null;
  }
}
