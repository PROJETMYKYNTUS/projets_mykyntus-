import { Injectable, signal } from '@angular/core';

export type KyntusPromptOptions = {
  title?: string;
  message?: string;
  defaultValue?: string;
  placeholder?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Si true (défaut), le bouton OK reste désactivé tant que la valeur est vide. */
  required?: boolean;
};

type KyntusPromptState = {
  visible: true;
  title: string;
  message: string | null;
  placeholder: string;
  confirmLabel: string;
  cancelLabel: string;
  required: boolean;
  value: string;
};

@Injectable({ providedIn: 'root' })
export class KyntusPromptService {
  readonly state = signal<KyntusPromptState | null>(null);

  private resolver: ((value: string | null) => void) | null = null;

  prompt(options: KyntusPromptOptions): Promise<string | null> {
    this.abortPending();

    return new Promise<string | null>((resolve) => {
      this.resolver = resolve;
      this.state.set({
        visible: true,
        title: options.title?.trim() || 'Saisie',
        message: options.message?.trim() || null,
        placeholder: options.placeholder?.trim() || '',
        confirmLabel: options.confirmLabel?.trim() || 'OK',
        cancelLabel: options.cancelLabel?.trim() || 'Annuler',
        required: options.required !== false,
        value: options.defaultValue ?? '',
      });
    });
  }

  setValue(value: string): void {
    const current = this.state();
    if (!current) return;
    this.state.set({ ...current, value });
  }

  canAccept(): boolean {
    const current = this.state();
    if (!current) return false;
    if (!current.required) return true;
    return current.value.trim().length > 0;
  }

  accept(): void {
    const current = this.state();
    if (!current || !this.canAccept()) return;
    this.resolver?.(current.value.trim());
    this.clear();
  }

  reject(): void {
    this.resolver?.(null);
    this.clear();
  }

  private abortPending(): void {
    if (this.resolver) {
      this.resolver(null);
      this.resolver = null;
    }
  }

  private clear(): void {
    this.resolver = null;
    this.state.set(null);
  }
}
