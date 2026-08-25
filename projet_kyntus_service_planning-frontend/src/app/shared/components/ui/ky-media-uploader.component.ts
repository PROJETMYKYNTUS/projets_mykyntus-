import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  inject,
} from '@angular/core';
import { ImagePlus, FileUp, X, Film, FileText } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';
import { MediaAsset, MediaService } from '../../../core/services/media.service';
import { formatHttpErrorMessage } from '../../../core/lib/http-error-message.util';
import { Subscription, forkJoin } from 'rxjs';

@Component({
  selector: 'app-ky-media-uploader',
  standalone: true,
  imports: [CommonModule, LucideIconComponent],
  template: `
    <div
      class="ky-mu"
      [class.ky-mu-drag]="dragging"
      (dragover)="onDragOver($event)"
      (dragleave)="dragging=false"
      (drop)="onDrop($event)">
      <input
        #fileInput
        type="file"
        class="ky-mu-input"
        [attr.accept]="accept"
        [attr.capture]="capture ? 'environment' : null"
        multiple
        (change)="onFilesSelected($event)" />
      <div class="ky-mu-actions">
        <button type="button" class="ky-btn-secondary" (click)="fileInput.click()" [disabled]="busy || disabled">
          <app-lucide-icon [icon]="icons.upload" className="w-4 h-4" />
          Ajouter des fichiers
        </button>
        <button
          *ngIf="allowCamera"
          type="button"
          class="ky-btn-secondary"
          (click)="openCamera(fileInput)"
          [disabled]="busy || disabled">
          <app-lucide-icon [icon]="icons.camera" className="w-4 h-4" />
          Caméra
        </button>
      </div>
      <p class="ky-mu-hint">{{ hint }}</p>
      <p class="ky-mu-error" *ngIf="error">{{ error }}</p>
      <p class="ky-mu-busy" *ngIf="busy">Envoi en cours…</p>

      <div class="ky-mu-list" *ngIf="items.length">
        <article class="ky-mu-item" *ngFor="let item of items">
          <span class="ky-mu-kind">
            <app-lucide-icon
              [icon]="item.kind === 'Video' ? icons.video : item.kind === 'Document' ? icons.doc : icons.image"
              className="w-4 h-4" />
          </span>
          <div class="ky-mu-meta">
            <strong>{{ item.fileName }}</strong>
            <small>{{ formatSize(item.sizeBytes) }} · {{ item.kind }}</small>
          </div>
          <button type="button" class="ky-mu-remove" (click)="remove(item)" [disabled]="busy || disabled" title="Retirer">
            <app-lucide-icon [icon]="icons.close" className="w-4 h-4" />
          </button>
        </article>
      </div>
    </div>
  `,
  styles: [`
    .ky-mu {
      border: 1.5px dashed var(--border-color);
      border-radius: var(--radius-card, 0.875rem);
      padding: 16px;
      background: var(--bg-input, var(--bg-card));
      display: grid;
      gap: 12px;
    }
    .ky-mu-drag { border-color: var(--blue-600); background: color-mix(in srgb, var(--soft-blue) 12%, var(--bg-card)); }
    .ky-mu-input { display: none; }
    .ky-mu-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .ky-mu-hint { margin: 0; font-size: 0.82rem; color: var(--text-muted); }
    .ky-mu-error { margin: 0; color: var(--danger); font-size: 0.85rem; }
    .ky-mu-busy { margin: 0; color: var(--text-muted); font-size: 0.85rem; }
    .ky-mu-list { display: grid; gap: 8px; }
    .ky-mu-item {
      display: flex; align-items: center; gap: 10px;
      padding: 10px 12px; border-radius: var(--radius-md, 0.5rem);
      background: var(--bg-card); border: 1px solid var(--border-color);
    }
    .ky-mu-meta { flex: 1; min-width: 0; display: grid; gap: 2px; }
    .ky-mu-meta strong { font-size: 0.88rem; color: var(--text-primary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .ky-mu-meta small { color: var(--text-muted); font-size: 0.75rem; }
    .ky-mu-remove { border: 0; background: transparent; cursor: pointer; color: var(--text-muted); }
    .ky-mu-kind { color: var(--blue-600); display: inline-flex; }
  `]
})
export class KyMediaUploaderComponent implements OnDestroy {
  private readonly mediaSvc = inject(MediaService);
  private sub?: Subscription;

  readonly icons = {
    upload: FileUp,
    camera: ImagePlus,
    close: X,
    video: Film,
    doc: FileText,
    image: ImagePlus,
  };

  @Input() accept = 'image/*,video/mp4,video/webm,application/pdf';
  @Input() hint = 'Photos, vidéos (MP4/WebM) ou PDF — plusieurs fichiers possibles';
  @Input() allowCamera = true;
  @Input() disabled = false;
  @Input() items: MediaAsset[] = [];
  @Output() itemsChange = new EventEmitter<MediaAsset[]>();

  busy = false;
  error: string | null = null;
  dragging = false;
  capture = false;

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  openCamera(input: HTMLInputElement): void {
    this.capture = true;
    input.setAttribute('accept', 'image/*');
    input.click();
    queueMicrotask(() => {
      this.capture = false;
      input.setAttribute('accept', this.accept);
    });
  }

  onDragOver(ev: DragEvent): void {
    ev.preventDefault();
    this.dragging = true;
  }

  onDrop(ev: DragEvent): void {
    ev.preventDefault();
    this.dragging = false;
    const files = Array.from(ev.dataTransfer?.files ?? []);
    this.uploadFiles(files);
  }

  onFilesSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.uploadFiles(files);
  }

  remove(item: MediaAsset): void {
    this.busy = true;
    this.mediaSvc.delete(item.id).subscribe({
      next: () => {
        this.items = this.items.filter(x => x.id !== item.id);
        this.itemsChange.emit(this.items);
        this.busy = false;
      },
      error: () => {
        // Orphan may already be gone — still remove locally
        this.items = this.items.filter(x => x.id !== item.id);
        this.itemsChange.emit(this.items);
        this.busy = false;
      }
    });
  }

  formatSize(n: number): string {
    if (n < 1024) return `${n} o`;
    if (n < 1_000_000) return `${(n / 1024).toFixed(0)} Ko`;
    return `${(n / 1_000_000).toFixed(1)} Mo`;
  }

  private uploadFiles(files: File[]): void {
    if (!files.length || this.disabled) return;
    this.error = null;
    this.busy = true;
    this.sub?.unsubscribe();
    this.sub = forkJoin(files.map(f => this.mediaSvc.upload(f))).subscribe({
      next: uploaded => {
        this.items = [...this.items, ...uploaded];
        this.itemsChange.emit(this.items);
        this.busy = false;
      },
      error: err => {
        this.error = formatHttpErrorMessage(err, 'Échec de l’envoi du fichier.');
        this.busy = false;
      }
    });
  }
}
