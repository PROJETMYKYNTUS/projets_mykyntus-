import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import {
  AudienceTarget,
  CampaignAnalytics,
  CampaignResponse,
  NewsletterResponse,
  NewsletterService
} from '../../core/services/newsletter.service';

type AdminView = 'list' | 'create' | 'campaigns' | 'analytics';

@Component({
  selector: 'app-newsletter-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './newsletter-admin.component.html',
  styleUrls: ['./newsletter-admin.component.css']
})
export class NewsletterAdminComponent implements OnInit, OnDestroy {
  currentView: AdminView = 'list';
  private destroy$ = new Subject<void>();
  private loadedViews = new Set<AdminView>();

  newsletters: NewsletterResponse[] = [];
  campaigns: CampaignResponse[] = [];
  analytics: CampaignAnalytics | null = null;
  previewNewsletter: NewsletterResponse | null = null;

  loadingNewsletters = false;
  loadingCampaigns = false;
  loadingAnalytics = false;
  submittingNewsletter = false;
  submittingCampaign = false;

  toast = { show: false, message: '', type: 'success' as 'success' | 'error' };

  audienceOptions: AudienceTarget[] = [
  'All', 'Employees', 'Managers', 'Admins',
  'Pilotes', 'Coaches', 'RPs', 'Audits',
  'EquipeFormation', 'Custom'
];

  newNewsletter = {
    title: '',
    subject: '',
    htmlContent: '',
    textContent: ''
  };

  newCampaign = {
    name: '',
    newsletterId: 0,
    audienceTarget: 'All' as AudienceTarget,
    scheduledAt: ''
  };

  selectedCampaignId: number | null = null;

  constructor(private newsletterSvc: NewsletterService) {}

  ngOnInit(): void {
    this.setView('list');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setView(view: AdminView): void {
    this.currentView = view;

    if (view === 'list' && !this.loadedViews.has('list')) {
      this.loadNewsletters();
    }

    if (view === 'campaigns' && !this.loadedViews.has('campaigns')) {
      this.loadCampaigns();
    }
  }

  refresh(): void {
    this.loadedViews.delete(this.currentView);
    if (this.currentView === 'analytics' && this.selectedCampaignId) {
      this.viewAnalytics(this.selectedCampaignId);
      return;
    }
    this.setView(this.currentView);
  }

  loadNewsletters(): void {
    this.loadingNewsletters = true;
    this.newsletterSvc.getNewsletters()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.newsletters = data ?? [];
          this.loadingNewsletters = false;
          this.loadedViews.add('list');
        },
        error: () => {
          this.loadingNewsletters = false;
          this.showToast('Erreur de chargement des newsletters', 'error');
        }
      });
  }

  loadCampaigns(): void {
    this.loadingCampaigns = true;
    this.newsletterSvc.getCampaigns()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.campaigns = data ?? [];
          this.loadingCampaigns = false;
          this.loadedViews.add('campaigns');
        },
        error: () => {
          this.loadingCampaigns = false;
          this.showToast('Erreur de chargement des campagnes', 'error');
        }
      });
  }

  submitNewsletter(): void {
    if (!this.newNewsletter.title.trim() || !this.newNewsletter.subject.trim() || !this.newNewsletter.htmlContent.trim()) {
      this.showToast('Remplissez les champs obligatoires de la newsletter', 'error');
      return;
    }

    this.submittingNewsletter = true;
    this.newsletterSvc.createNewsletter(this.newNewsletter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.submittingNewsletter = false;
          this.newNewsletter = { title: '', subject: '', htmlContent: '', textContent: '' };
          this.loadedViews.delete('list');
          this.setView('list');
          this.showToast('Newsletter creee avec succes');
        },
        error: () => {
          this.submittingNewsletter = false;
          this.showToast('Erreur lors de la creation de la newsletter', 'error');
        }
      });
  }

  deleteNewsletter(id: number): void {
    if (!confirm('Supprimer cette newsletter ?')) {
      return;
    }

    this.newsletterSvc.deleteNewsletter(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showToast('Newsletter supprimee');
          this.loadedViews.delete('list');
          this.loadNewsletters();
        },
        error: () => {
          this.showToast('Erreur lors de la suppression', 'error');
        }
      });
  }

  submitCampaign(): void {
    if (!this.newCampaign.name.trim() || !this.newCampaign.newsletterId) {
      this.showToast('Selectionnez une newsletter et donnez un nom a la campagne', 'error');
      return;
    }

    this.submittingCampaign = true;
    const dto = {
      ...this.newCampaign,
      scheduledAt: this.newCampaign.scheduledAt || null
    };

    this.newsletterSvc.createCampaign(dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.submittingCampaign = false;
          this.newCampaign = {
            name: '',
            newsletterId: 0,
            audienceTarget: 'All',
            scheduledAt: ''
          };
          this.loadedViews.delete('campaigns');
          this.setView('campaigns');
          this.showToast('Campagne creee avec succes');
        },
        error: () => {
          this.submittingCampaign = false;
          this.showToast('Erreur lors de la creation de la campagne', 'error');
        }
      });
  }

  publishCampaign(id: number): void {
    if (!confirm('Publier cette campagne maintenant ?')) {
      return;
    }

    this.newsletterSvc.publishCampaign(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showToast('Campagne publiee');
          this.loadedViews.delete('campaigns');
          this.loadCampaigns();
        },
        error: () => {
          this.showToast('Erreur lors de la publication', 'error');
        }
      });
  }

  cancelCampaign(id: number): void {
    if (!confirm('Annuler cette campagne ?')) {
      return;
    }

    this.newsletterSvc.cancelCampaign(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showToast('Campagne annulee');
          this.loadedViews.delete('campaigns');
          this.loadCampaigns();
        },
        error: () => {
          this.showToast('Erreur lors de l annulation', 'error');
        }
      });
  }

  viewAnalytics(id: number): void {
    this.selectedCampaignId = id;
    this.loadingAnalytics = true;
    this.newsletterSvc.getAnalytics(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.analytics = data;
          this.loadingAnalytics = false;
          this.currentView = 'analytics';
          this.loadedViews.add('analytics');
        },
        error: () => {
          this.loadingAnalytics = false;
          this.showToast('Erreur de chargement des analytics', 'error');
        }
      });
  }

  openNewsletterPreview(newsletter: NewsletterResponse): void {
    this.previewNewsletter = newsletter;
  }

  closeNewsletterPreview(): void {
    this.previewNewsletter = null;
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Draft: 'nl-status-draft',
      Scheduled: 'nl-status-scheduled',
      Sending: 'nl-status-sending',
      Sent: 'nl-status-sent',
      Cancelled: 'nl-status-cancelled',
      Failed: 'nl-status-failed'
    };
    return map[status] || 'nl-status-draft';
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Draft: 'Brouillon',
      Scheduled: 'Planifiee',
      Sending: 'En cours',
      Sent: 'Envoyee',
      Cancelled: 'Annulee',
      Failed: 'Echec'
    };
    return map[status] || status;
  }

getAudienceLabel(audience: string): string {
    const map: Record<string, string> = {
        All:             'Tous',
        Employees:       'Employés',
        Managers:        'Managers',
        Admins:          'Admins',
        Pilotes:         'Pilotes',
        Coaches:         'Coachs',
        RPs:             'RP',
        Audits:          'Audit',
        EquipeFormation: 'Équipe formation',
        Custom:          'Personnalisé'
    };
    return map[audience] || audience;
}
  getAnalyticsReadPercent(): number {
    return this.analytics ? Math.max(0, Math.min(100, this.analytics.readRate || 0)) : 0;
  }

  formatDate(value?: string | null): string {
    if (!value) {
      return '-';
    }

    return new Date(value).toLocaleString('fr-FR', {
      year: 'numeric',
      month: 'short',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  showToast(message: string, type: 'success' | 'error' = 'success'): void {
    this.toast = { show: true, message, type };
    setTimeout(() => {
      this.toast.show = false;
    }, 3500);
  }

  trackById(index: number, item: { id: number }): number {
    return item.id;
  }
}
