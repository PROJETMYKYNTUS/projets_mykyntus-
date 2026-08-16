import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  ViewChild,
  forwardRef,
  inject,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Bold, ImagePlus, Italic, Link2, List, ListOrdered } from 'lucide';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { compressImageToDataUrl } from '../../../core/lib/formation-learning-html.util';
import { KyntusPromptService } from '../../../shared/components/kyntus-prompt/kyntus-prompt.service';

@Component({
  selector: 'app-formation-rich-text-editor',
  standalone: true,
  imports: [CommonModule, LucideIconComponent],
  templateUrl: './formation-rich-text-editor.component.html',
  styleUrls: ['./formation-rich-text-editor.component.css'],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => FormationRichTextEditorComponent),
      multi: true,
    },
  ],
})
export class FormationRichTextEditorComponent implements ControlValueAccessor, OnChanges {
  private readonly promptService = inject(KyntusPromptService);

  readonly icons = {
    bold: Bold,
    italic: Italic,
    list: List,
    ordered: ListOrdered,
    link: Link2,
    image: ImagePlus,
  };

  @Input() placeholder = 'Rédigez le contenu (paragraphes, listes, images…)';
  @Input() disabled = false;
  @Output() readonly valueChange = new EventEmitter<string>();

  @ViewChild('editor', { static: true }) editorRef!: ElementRef<HTMLDivElement>;
  @ViewChild('fileInput', { static: true }) fileInputRef!: ElementRef<HTMLInputElement>;

  private onChange: (v: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;
  private lastHtml = '';
  busyImage = false;
  imageError: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['disabled'] && this.editorRef) {
      this.editorRef.nativeElement.contentEditable = this.disabled ? 'false' : 'true';
    }
  }

  writeValue(value: string | null): void {
    const html = value ?? '';
    this.lastHtml = html;
    queueMicrotask(() => {
      const el = this.editorRef?.nativeElement;
      if (!el) return;
      if (el.innerHTML !== html) {
        el.innerHTML = html || '';
      }
    });
  }

  registerOnChange(fn: (v: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    if (this.editorRef) {
      this.editorRef.nativeElement.contentEditable = isDisabled ? 'false' : 'true';
    }
  }

  onInput(): void {
    const html = this.editorRef.nativeElement.innerHTML;
    this.lastHtml = html;
    this.onChange(html);
    this.valueChange.emit(html);
  }

  onBlur(): void {
    this.onTouched();
  }

  exec(cmd: string, value?: string): void {
    if (this.disabled) return;
    this.editorRef.nativeElement.focus();
    document.execCommand(cmd, false, value);
    this.onInput();
  }

  async addLink(): Promise<void> {
    if (this.disabled) return;
    const url = await this.promptService.prompt({
      title: 'URL du lien',
      defaultValue: 'https://',
      placeholder: 'https://',
      confirmLabel: 'Insérer',
    });
    if (!url?.trim()) return;
    this.exec('createLink', url.trim());
  }

  pickImage(): void {
    if (this.disabled || this.busyImage) return;
    this.fileInputRef.nativeElement.click();
  }

  async onImageSelected(ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.imageError = 'Choisissez un fichier image.';
      return;
    }
    this.busyImage = true;
    this.imageError = null;
    try {
      const dataUrl = await compressImageToDataUrl(file);
      this.editorRef.nativeElement.focus();
      document.execCommand('insertHTML', false, `<p><img src="${dataUrl}" alt="" /></p>`);
      this.onInput();
    } catch (e) {
      this.imageError = e instanceof Error ? e.message : 'Insertion image impossible';
    } finally {
      this.busyImage = false;
    }
  }
}
