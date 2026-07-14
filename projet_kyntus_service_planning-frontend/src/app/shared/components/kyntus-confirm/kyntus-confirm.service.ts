import { Injectable, signal } from '@angular/core';

export type KyntusConfirmVariant = 'warning' | 'danger' | 'default';

export type KyntusConfirmChoice = {
  id: string;
  label: string;
  /** Pré-coché (défaut : true). */
  checked?: boolean;
};

export type KyntusConfirmOptions = {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: KyntusConfirmVariant;
};

export type KyntusConfirmSelectOptions = KyntusConfirmOptions & {
  choices: readonly KyntusConfirmChoice[];
  /** Texte d’aide sous la liste. */
  choicesHint?: string;
  /** Au moins une case cochée pour activer Confirmer (défaut : true). */
  requireSelection?: boolean;
};

type KyntusConfirmState = KyntusConfirmOptions & {
  visible: true;
  title: string;
  confirmLabel: string;
  cancelLabel: string;
  variant: KyntusConfirmVariant;
  choices: KyntusConfirmChoice[];
  choicesHint: string | null;
  requireSelection: boolean;
  selectedIds: string[];
  mode: 'boolean' | 'select';
};

@Injectable({ providedIn: 'root' })
export class KyntusConfirmService {
  readonly state = signal<KyntusConfirmState | null>(null);

  private booleanResolver: ((accepted: boolean) => void) | null = null;
  private selectResolver: ((selectedIds: string[] | null) => void) | null = null;

  confirm(options: KyntusConfirmOptions): Promise<boolean> {
    this.abortPending();

    return new Promise<boolean>((resolve) => {
      this.booleanResolver = resolve;
      this.state.set({
        visible: true,
        title: options.title?.trim() || 'Confirmation',
        message: options.message,
        confirmLabel: options.confirmLabel?.trim() || 'Confirmer',
        cancelLabel: options.cancelLabel?.trim() || 'Annuler',
        variant: options.variant ?? 'warning',
        choices: [],
        choicesHint: null,
        requireSelection: false,
        selectedIds: [],
        mode: 'boolean',
      });
    });
  }

  /**
   * Dialogue avec cases à cocher. Retourne les ids sélectionnés, ou null si annulé.
   */
  confirmSelect(options: KyntusConfirmSelectOptions): Promise<string[] | null> {
    this.abortPending();

    const choices = options.choices.map((c) => ({
      id: c.id,
      label: c.label,
      checked: c.checked !== false,
    }));
    const selectedIds = choices.filter((c) => c.checked).map((c) => c.id);

    return new Promise<string[] | null>((resolve) => {
      this.selectResolver = resolve;
      this.state.set({
        visible: true,
        title: options.title?.trim() || 'Confirmation',
        message: options.message,
        confirmLabel: options.confirmLabel?.trim() || 'Confirmer',
        cancelLabel: options.cancelLabel?.trim() || 'Annuler',
        variant: options.variant ?? 'warning',
        choices,
        choicesHint: options.choicesHint?.trim() || null,
        requireSelection: options.requireSelection !== false,
        selectedIds,
        mode: 'select',
      });
    });
  }

  toggleChoice(id: string, checked: boolean): void {
    const current = this.state();
    if (!current || current.mode !== 'select') return;
    const selected = new Set(current.selectedIds);
    if (checked) selected.add(id);
    else selected.delete(id);
    this.state.set({ ...current, selectedIds: [...selected] });
  }

  canAccept(): boolean {
    const current = this.state();
    if (!current) return false;
    if (current.mode === 'select' && current.requireSelection) {
      return current.selectedIds.length > 0;
    }
    return true;
  }

  accept(): void {
    const current = this.state();
    if (!current) return;
    if (current.mode === 'select') {
      if (current.requireSelection && current.selectedIds.length === 0) return;
      this.selectResolver?.([...current.selectedIds]);
    } else {
      this.booleanResolver?.(true);
    }
    this.clear();
  }

  reject(): void {
    if (this.selectResolver) this.selectResolver(null);
    else this.booleanResolver?.(false);
    this.clear();
  }

  private abortPending(): void {
    if (this.selectResolver) {
      this.selectResolver(null);
      this.selectResolver = null;
    }
    if (this.booleanResolver) {
      this.booleanResolver(false);
      this.booleanResolver = null;
    }
  }

  private clear(): void {
    this.booleanResolver = null;
    this.selectResolver = null;
    this.state.set(null);
  }
}
