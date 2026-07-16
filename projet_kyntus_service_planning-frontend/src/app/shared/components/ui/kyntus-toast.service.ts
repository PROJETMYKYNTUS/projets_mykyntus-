import { Injectable, signal } from '@angular/core';

export type KyntusToastKind = 'success' | 'error' | 'info';

export interface KyntusToast {
  id: number;
  kind: KyntusToastKind;
  message: string;
  durationMs: number;
}

@Injectable({ providedIn: 'root' })
export class KyntusToastService {
  private seq = 0;
  readonly active = signal<KyntusToast | null>(null);
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  show(message: string, kind: KyntusToastKind = 'info', durationMs = 4000): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
      this.hideTimer = null;
    }
    const toast: KyntusToast = { id: ++this.seq, kind, message, durationMs };
    this.active.set(toast);
    this.hideTimer = setTimeout(() => this.dismiss(), durationMs);
  }

  success(message: string, durationMs = 4000): void {
    this.show(message, 'success', durationMs);
  }

  error(message: string, durationMs = 5000): void {
    this.show(message, 'error', durationMs);
  }

  info(message: string, durationMs = 4000): void {
    this.show(message, 'info', durationMs);
  }

  dismiss(): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
      this.hideTimer = null;
    }
    this.active.set(null);
  }
}
