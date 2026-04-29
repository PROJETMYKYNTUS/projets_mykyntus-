import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type DocumentationToastKind = 'error' | 'success';

export interface DocumentationToast {
  readonly text: string;
  readonly kind: DocumentationToastKind;
}

@Injectable({ providedIn: 'root' })
export class DocumentationNotificationService {
  private readonly toastSubject = new BehaviorSubject<DocumentationToast | null>(null);
  private clearTimer: ReturnType<typeof setTimeout> | null = null;
  readonly toast$ = this.toastSubject.asObservable();

  showError(text: string): void {
    this.show({ text, kind: 'error' });
  }

  showSuccess(text: string): void {
    this.show({ text, kind: 'success' });
  }

  clear(): void {
    if (this.clearTimer) {
      clearTimeout(this.clearTimer);
      this.clearTimer = null;
    }
    this.toastSubject.next(null);
  }

  private show(toast: DocumentationToast): void {
    this.clear();
    this.toastSubject.next(toast);
    this.clearTimer = setTimeout(() => this.clear(), toast.kind === 'error' ? 7000 : 4500);
  }
}
