import { Injectable, signal } from '@angular/core';

export type KyntusConfirmVariant = 'warning' | 'danger' | 'default';

export type KyntusConfirmOptions = {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: KyntusConfirmVariant;
};

type KyntusConfirmState = KyntusConfirmOptions & {
  visible: true;
  title: string;
  confirmLabel: string;
  cancelLabel: string;
  variant: KyntusConfirmVariant;
};

@Injectable({ providedIn: 'root' })
export class KyntusConfirmService {
  readonly state = signal<KyntusConfirmState | null>(null);

  private resolver: ((accepted: boolean) => void) | null = null;

  confirm(options: KyntusConfirmOptions): Promise<boolean> {
    if (this.resolver) {
      this.resolver(false);
    }

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
      this.state.set({
        visible: true,
        title: options.title?.trim() || 'Confirmation',
        message: options.message,
        confirmLabel: options.confirmLabel?.trim() || 'Confirmer',
        cancelLabel: options.cancelLabel?.trim() || 'Annuler',
        variant: options.variant ?? 'warning',
      });
    });
  }

  accept(): void {
    this.resolver?.(true);
    this.clear();
  }

  reject(): void {
    this.resolver?.(false);
    this.clear();
  }

  private clear(): void {
    this.resolver = null;
    this.state.set(null);
  }
}
