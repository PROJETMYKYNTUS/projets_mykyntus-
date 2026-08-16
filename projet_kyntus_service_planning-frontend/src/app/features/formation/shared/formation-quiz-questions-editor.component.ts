import { CommonModule } from '@angular/common';
import { Component, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  emptyQuizDraftQuestion,
  isQuizMediaVideo,
  type QuizDraftQuestion,
} from './formation-quiz-draft.types';

export type QuizMediaUploadTarget =
  | { type: 'template'; templateId: string }
  | { type: 'session'; sessionId: string; animatorUserId: string };

@Component({
  selector: 'app-formation-quiz-questions-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './formation-quiz-questions-editor.component.html',
  styleUrls: ['./formation-quiz-questions-editor.component.css'],
})
export class FormationQuizQuestionsEditorComponent {
  readonly icons = { add: Plus, remove: Trash2, up: ArrowUp, down: ArrowDown };
  private readonly api = inject(FormationTrainingService);

  /** Questions en édition (two-way). */
  readonly questions = model<QuizDraftQuestion[]>([]);
  readonly disabled = input(false);
  readonly minQuestions = input(0);
  /** Active l’upload fichier (nécessite un id de question après 1er enregistrement). */
  readonly mediaUploadTarget = input<QuizMediaUploadTarget | null>(null);

  readonly uploadError = signal<string | null>(null);
  readonly uploadingQuestionId = signal<string | null>(null);

  addQuestion(): void {
    this.questions.update((list) => [...list, emptyQuizDraftQuestion()]);
  }

  removeQuestion(index: number): void {
    const min = this.minQuestions();
    this.questions.update((list) => {
      if (list.length <= min) return list;
      return list.filter((_, i) => i !== index);
    });
  }

  moveQuestion(index: number, delta: number): void {
    this.questions.update((list) => {
      const target = index + delta;
      if (target < 0 || target >= list.length) return list;
      const copy = [...list];
      const [item] = copy.splice(index, 1);
      copy.splice(target, 0, item);
      return copy;
    });
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

  canUpload(q: QuizDraftQuestion): boolean {
    return !!this.mediaUploadTarget() && !!q.id && !this.disabled();
  }

  isVideo(q: QuizDraftQuestion): boolean {
    return isQuizMediaVideo(q.imageUrl, q.mediaKind);
  }

  clearMedia(q: QuizDraftQuestion): void {
    q.imageUrl = '';
    q.mediaKind = null;
  }

  async onMediaFileSelected(q: QuizDraftQuestion, ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const target = this.mediaUploadTarget();
    if (!target || !q.id) {
      this.uploadError.set('Enregistrez d’abord le quiz pour activer l’upload.');
      input.value = '';
      return;
    }

    this.uploadingQuestionId.set(q.id);
    this.uploadError.set(null);
    try {
      if (target.type === 'template') {
        const updated = await this.api.uploadQuizTemplateQuestionMedia(target.templateId, q.id, file);
        q.imageUrl = updated.imageUrl || q.imageUrl;
        q.mediaKind =
          updated.mediaKind === 'video' || file.type.startsWith('video/')
            ? 'video'
            : 'image';
      } else {
        const updated = await this.api.uploadQuizQuestionImage(
          target.sessionId,
          q.id,
          file,
          target.animatorUserId,
        );
        q.imageUrl = updated.imageUrl || q.imageUrl;
        q.mediaKind = file.type.startsWith('video/') ? 'video' : 'image';
      }
    } catch (e) {
      this.uploadError.set(e instanceof Error ? e.message : 'Upload impossible');
    } finally {
      this.uploadingQuestionId.set(null);
      input.value = '';
    }
  }

  trackByIndex(index: number): number {
    return index;
  }
}
