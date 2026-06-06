import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { DomSanitizer, type SafeResourceUrl } from '@angular/platform-browser';
import { Download, Expand, ExternalLink, FileText } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

@Component({
  selector: 'app-cv-preview-panel',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="space-y-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-sm font-semibold text-primary">Visualisation du CV</h2>
        @if (cvUrl()) {
          <div class="flex flex-wrap gap-2">
            @if (isPdf()) {
              <button
                type="button"
                (click)="fullscreen.set(true)"
                class="inline-flex items-center gap-1.5 rounded-lg border border-default px-3 py-1.5 text-xs text-primary hover:bg-input"
              >
                <app-lucide-icon [icon]="expandIcon" className="h-3.5 w-3.5" />
                Plein écran
              </button>
            }
            <a
              [href]="downloadUrl()"
              target="_blank"
              rel="noopener"
              class="inline-flex items-center gap-1.5 rounded-lg border border-default px-3 py-1.5 text-xs text-primary hover:bg-input"
            >
              <app-lucide-icon [icon]="externalIcon" className="h-3.5 w-3.5" />
              Ouvrir
            </a>
            <a
              [href]="downloadUrl()"
              download
              class="inline-flex items-center gap-1.5 rounded-lg border border-default px-3 py-1.5 text-xs text-primary hover:bg-input"
            >
              <app-lucide-icon [icon]="downloadIcon" className="h-3.5 w-3.5" />
              Télécharger
            </a>
          </div>
        }
      </div>

      @if (cvPreviewUrl(); as src) {
        <iframe
          title="Aperçu CV"
          [src]="src"
          class="w-full rounded-lg border border-default bg-card"
          style="height: 420px"
        ></iframe>
      } @else if (cvUrl()) {
        <div class="rounded-lg border border-default bg-card/50 p-8 text-center space-y-3">
          <app-lucide-icon [icon]="fileIcon" className="h-10 w-10 text-muted mx-auto" />
          <p class="text-sm font-medium text-primary">Aperçu non disponible pour ce format</p>
          <p class="text-xs text-muted">Les fichiers Word (DOC/DOCX) doivent être téléchargés pour consultation.</p>
          <a
            [href]="downloadUrl()"
            download
            class="inline-flex items-center gap-2 rounded-lg bg-soft-blue/20 px-4 py-2 text-sm text-soft-blue hover:bg-soft-blue/30"
          >
            <app-lucide-icon [icon]="downloadIcon" className="h-4 w-4" />
            Télécharger le CV
          </a>
        </div>
      } @else {
        <div class="rounded-lg border border-default bg-card/50 p-10 text-center">
          <p class="text-sm font-medium text-primary">Aucun CV téléchargé</p>
          <p class="text-xs text-muted mt-2">Veuillez vérifier le dossier RH avant décision.</p>
        </div>
      }
    </div>

    @if (fullscreen()) {
      <div class="fixed inset-0 z-[70] bg-black/80 flex flex-col">
        <div class="flex items-center justify-between px-4 py-3 border-b border-default bg-app">
          <span class="text-sm text-primary">Aperçu CV — plein écran</span>
          <button type="button" (click)="fullscreen.set(false)" class="text-sm text-muted hover:text-white">Fermer</button>
        </div>
        @if (cvPreviewUrl(); as src) {
          <iframe title="CV plein écran" [src]="src" class="flex-1 w-full bg-white"></iframe>
        }
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CvPreviewPanelComponent {
  readonly cvUrl = input<string | undefined>(undefined);

  private readonly sanitizer = inject(DomSanitizer);

  readonly expandIcon = Expand;
  readonly downloadIcon = Download;
  readonly externalIcon = ExternalLink;
  readonly fileIcon = FileText;
  readonly fullscreen = signal(false);

  readonly isPdf = computed(() => {
    const url = this.cvUrl()?.toLowerCase() ?? '';
    return url.includes('.pdf') || !url.includes('.doc');
  });

  readonly cvPreviewUrl = computed((): SafeResourceUrl | null => {
    const url = this.cvUrl();
    if (!url || !this.isPdf()) return null;
    const resolved = url.includes('?') ? `${url}&disposition=inline` : `${url}?disposition=inline`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(resolved);
  });

  downloadUrl(): string {
    const url = this.cvUrl();
    if (!url) return '#';
    return url.includes('?') ? `${url}&disposition=attachment` : `${url}?disposition=attachment`;
  }
}
