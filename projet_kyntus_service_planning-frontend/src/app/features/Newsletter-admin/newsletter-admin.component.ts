import { Component, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { KyntusPageHeaderComponent } from '../../shared/components/ui/kyntus-page-header.component';
import { KyntusToastService } from '../../shared/components/ui/kyntus-toast.service';
import { KyMediaUploaderComponent } from '../../shared/components/ui/ky-media-uploader.component';
import { KyRichTextEditorComponent } from '../../shared/components/ui/ky-rich-text-editor.component';
import { MediaAsset } from '../../core/services/media.service';
import {
  AudienceTarget,
  CampaignAnalytics,
  CampaignResponse,
  NewsletterService
} from '../../core/services/newsletter.service';
import { NewsletterReaderComponent } from '../newsletter-inbox/newsletter-reader.component';
import {
  KyntusAudiencePickerComponent,
  type AudiencePickerSelection,
} from '../formation/shared/kyntus-audience-picker.component';
import type { EmployeePickerRow } from '../contract/lib/contract-employee-filter';
import { resolveUserGuid } from '../../core/lib/user-guid.util';
import { formatHttpErrorMessage } from '../../core/lib/http-error-message.util';

type AdminMode = 'compose' | 'suivi';

@Component({
  selector: 'app-newsletter-admin',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    KyntusPageHeaderComponent,
    KyMediaUploaderComponent,
    KyRichTextEditorComponent,
    NewsletterReaderComponent,
    KyntusAudiencePickerComponent,
  ],
  templateUrl: './newsletter-admin.component.html',
  styleUrls: ['./newsletter-admin.component.css']
})
export class NewsletterAdminComponent implements OnInit, OnDestroy {
  private readonly toastSvc = inject(KyntusToastService);
  private destroy$ = new Subject<void>();

  @ViewChild(KyRichTextEditorComponent) editor?: KyRichTextEditorComponent;

  mode: AdminMode = 'compose';
  campaigns: CampaignResponse[] = [];
  analytics: CampaignAnalytics | null = null;
  selectedCampaignId: number | null = null;
  loading = false;
  submitting = false;

  form = {
    title: '',
    subject: '',
    textContent: '',
    scheduleMode: 'now' as 'now' | 'later' | 'draft',
    scheduledAt: '',
  };

  mediaItems: MediaAsset[] = [];
  readonly beneficiaryList = signal<EmployeePickerRow[]>([]);

  constructor(private newsletterSvc: NewsletterService) {}

  ngOnInit(): void {
    this.loadCampaigns();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setMode(mode: AdminMode): void {
    this.mode = mode;
    if (mode === 'suivi') this.loadCampaigns();
  }

  loadCampaigns(): void {
    this.loading = true;
    this.newsletterSvc.getCampaigns()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: list => {
          this.campaigns = list;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.toastSvc.error('Impossible de charger le suivi.');
        }
      });
  }

  onBeneficiariesChange(sel: AudiencePickerSelection): void {
    this.beneficiaryList.set([...sel.beneficiaries]);
  }

  getAudienceLabel(a: AudienceTarget): string {
    const map: Record<string, string> = {
      All: 'Tous', Employees: 'Employés', Managers: 'Managers', Admins: 'Admins',
      Pilotes: 'Pilotes', Coaches: 'Coaches', RPs: 'RPs', Audits: 'Audit',
      EquipeFormation: 'Équipe formation', Custom: 'Destinataires sélectionnés'
    };
    return map[a] ?? 'Destinataires';
  }

  getStatusLabel(s: string): string {
    const map: Record<string, string> = {
      Draft: 'Brouillon', Scheduled: 'Planifiée', Sending: 'Envoi…',
      Sent: 'Envoyée', Cancelled: 'Annulée', Failed: 'Échec'
    };
    return map[s] ?? 'Inconnu';
  }

  formatDate(iso?: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('fr-FR');
  }

  submit(mode: 'draft' | 'publish' | 'schedule'): void {
    const text = this.editor?.getPlainText() || this.form.textContent.trim();
    if (!this.form.title.trim()) {
      this.toastSvc.error('Le titre est obligatoire.');
      return;
    }
    if (!text) {
      this.toastSvc.error('Le message est obligatoire.');
      return;
    }
    if (mode === 'schedule' && !this.form.scheduledAt) {
      this.toastSvc.error('Choisissez une date de planification.');
      return;
    }

    const beneficiaryUserIds = this.beneficiaryList()
      .map(r => resolveUserGuid(r.user))
      .filter(id => !!id);

    if (mode !== 'draft' && beneficiaryUserIds.length === 0) {
      this.toastSvc.error('Sélectionnez au moins un destinataire.');
      return;
    }

    this.submitting = true;
    this.newsletterSvc.createPublication({
      title: this.form.title.trim(),
      subject: (this.form.subject || this.form.title).trim(),
      textContent: text,
      mediaIds: this.mediaItems.map(m => m.id),
      audienceTarget: 'Custom',
      beneficiaryUserIds,
      mode,
      scheduledAt: mode === 'schedule' ? new Date(this.form.scheduledAt).toISOString() : null,
      campaignName: this.form.title.trim(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.submitting = false;
        this.toastSvc.success(
          mode === 'draft' ? 'Brouillon enregistré.'
            : mode === 'schedule' ? 'Publication planifiée.'
            : 'Publication envoyée.'
        );
        this.resetForm();
        this.setMode('suivi');
      },
      error: err => {
        this.submitting = false;
        this.toastSvc.error(formatHttpErrorMessage(err, 'Échec de la publication.'));
      }
    });
  }

  resetForm(): void {
    this.form = {
      title: '',
      subject: '',
      textContent: '',
      scheduleMode: 'now',
      scheduledAt: '',
    };
    this.mediaItems = [];
    this.beneficiaryList.set([]);
    this.editor?.writeValue('');
  }

  openAnalytics(id: number): void {
    this.selectedCampaignId = id;
    this.newsletterSvc.getAnalytics(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: a => this.analytics = a,
      error: () => this.toastSvc.error('Impossible de charger le suivi des lectures.')
    });
  }

  publishExisting(id: number): void {
    this.newsletterSvc.publishCampaign(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastSvc.success('Publication envoyée.');
        this.loadCampaigns();
      },
      error: err => this.toastSvc.error(formatHttpErrorMessage(err, 'Publication impossible.'))
    });
  }

  cancelCampaign(id: number): void {
    this.newsletterSvc.cancelCampaign(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastSvc.success('Publication annulée.');
        this.loadCampaigns();
      },
      error: err => this.toastSvc.error(formatHttpErrorMessage(err, 'Annulation impossible.'))
    });
  }

  previewTitle(): string {
    return this.form.title.trim() || 'Titre de la publication';
  }

  previewSubject(): string {
    return (this.form.subject || this.form.title).trim() || 'Sujet';
  }

  previewText(): string {
    return this.editor?.getPlainText() || this.form.textContent || 'Aperçu du message…';
  }
}
