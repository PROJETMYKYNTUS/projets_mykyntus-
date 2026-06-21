import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AllowanceRejectDialogService {
  readonly visible = signal(false);
  readonly title = signal('Motif de rejet');

  private resolver: ((reason: string | null) => void) | null = null;

  open(title = 'Motif de rejet'): Promise<string | null> {
    if (this.resolver) {
      this.resolver(null);
    }
    this.title.set(title);
    return new Promise<string | null>((resolve) => {
      this.resolver = resolve;
      this.visible.set(true);
    });
  }

  confirm(reason: string): void {
    const trimmed = reason.trim();
    if (!trimmed) return;
    this.resolver?.(trimmed);
    this.clear();
  }

  cancel(): void {
    this.resolver?.(null);
    this.clear();
  }

  private clear(): void {
    this.resolver = null;
    this.visible.set(false);
  }
}
