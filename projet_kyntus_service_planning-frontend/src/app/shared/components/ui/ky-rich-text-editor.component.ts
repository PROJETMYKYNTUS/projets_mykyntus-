import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  Input,
  ViewChild,
  forwardRef,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Bold, Italic, List, ListOrdered, Link2 } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';

@Component({
  selector: 'app-ky-rich-text-editor',
  standalone: true,
  imports: [CommonModule, LucideIconComponent],
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => KyRichTextEditorComponent),
    multi: true
  }],
  template: `
    <div class="ky-rte" [class.disabled]="disabled">
      <div class="ky-rte-toolbar" role="toolbar">
        <button type="button" (click)="exec('bold')" [disabled]="disabled" title="Gras">
          <app-lucide-icon [icon]="icons.bold" className="w-4 h-4" />
        </button>
        <button type="button" (click)="exec('italic')" [disabled]="disabled" title="Italique">
          <app-lucide-icon [icon]="icons.italic" className="w-4 h-4" />
        </button>
        <button type="button" (click)="exec('insertUnorderedList')" [disabled]="disabled" title="Liste">
          <app-lucide-icon [icon]="icons.list" className="w-4 h-4" />
        </button>
        <button type="button" (click)="exec('insertOrderedList')" [disabled]="disabled" title="Liste numérotée">
          <app-lucide-icon [icon]="icons.ordered" className="w-4 h-4" />
        </button>
        <button type="button" (click)="addLink()" [disabled]="disabled" title="Lien">
          <app-lucide-icon [icon]="icons.link" className="w-4 h-4" />
        </button>
      </div>
      <div
        #editor
        class="ky-rte-editor"
        [attr.contenteditable]="disabled ? 'false' : 'true'"
        [attr.data-placeholder]="placeholder"
        (input)="onInput()"
        (blur)="onTouched()"></div>
    </div>
  `,
  styles: [`
    .ky-rte {
      border: 1px solid var(--border-color);
      border-radius: var(--radius-md, 0.5rem);
      overflow: hidden;
      background: var(--bg-input, var(--bg-card));
    }
    .ky-rte.disabled { opacity: 0.65; }
    .ky-rte-toolbar {
      display: flex; gap: 4px; padding: 8px;
      border-bottom: 1px solid var(--border-color);
      background: color-mix(in srgb, var(--soft-blue) 8%, var(--bg-card));
    }
    .ky-rte-toolbar button {
      border: 0; background: transparent; cursor: pointer;
      padding: 6px; border-radius: 6px; color: var(--text-primary);
    }
    .ky-rte-toolbar button:hover { background: color-mix(in srgb, var(--soft-blue) 18%, transparent); }
    .ky-rte-editor {
      min-height: 140px; padding: 12px 14px;
      color: var(--text-primary); line-height: 1.6; outline: none;
    }
    .ky-rte-editor:empty::before {
      content: attr(data-placeholder);
      color: var(--text-muted);
    }
  `]
})
export class KyRichTextEditorComponent implements ControlValueAccessor {
  readonly icons = { bold: Bold, italic: Italic, list: List, ordered: ListOrdered, link: Link2 };

  @Input() placeholder = 'Rédigez votre message…';
  @Input() disabled = false;
  @ViewChild('editor', { static: true }) editorRef!: ElementRef<HTMLDivElement>;

  private onChange: (v: string) => void = () => undefined;
  onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    const html = value ?? '';
    queueMicrotask(() => {
      const el = this.editorRef?.nativeElement;
      if (el && el.innerHTML !== html) el.innerHTML = html;
    });
  }

  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled = isDisabled; }

  exec(cmd: string): void {
    document.execCommand(cmd);
    this.onInput();
  }

  addLink(): void {
    const url = window.prompt('URL du lien');
    if (!url) return;
    document.execCommand('createLink', false, url);
    this.onInput();
  }

  onInput(): void {
    const text = this.editorRef.nativeElement.innerText ?? '';
    this.onChange(text);
  }

  /** Plain text for API (newsletter stores textContent). */
  getPlainText(): string {
    return this.editorRef?.nativeElement?.innerText?.trim() ?? '';
  }
}
