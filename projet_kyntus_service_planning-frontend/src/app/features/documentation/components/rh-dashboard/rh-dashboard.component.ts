import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { forkJoin, Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../../../core/services/documentation-identity.service';
import type { DocumentationRequest } from '../../interfaces/documentation-entities';
import { switchMapOnDocumentationContext } from '../../lib/documentation-context-refresh';
import { mapDocumentRequestDto } from '../../lib/documentation-dto-mappers';
import { DocumentationApiService } from '../../services/documentation-api.service';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-rh-dashboard',
  standalone: true,
  imports: [CommonModule, DocIconComponent, StatusBadgeComponent],
  templateUrl: './rh-dashboard.component.html',
})
export class RhDashboardComponent implements OnInit, OnDestroy {
  private sub = new Subscription();

  loading = true;
  error: string | null = null;
  requests: DocumentationRequest[] = [];
  stats: Array<{ label: string; value: number; icon: string; color: string; bg: string }> = [];

  constructor(
    private readonly api: DocumentationApiService,
    private readonly nav: DocumentationNavigationService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  ngOnInit(): void {
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () =>
        forkJoin({
          reqs: this.api.getDocumentRequestsPage(25, { sortBy: 'createdAt', sortOrder: 'desc' }),
          pending: this.api.getDocumentRequestsPage(1, { status: 'pending' }),
        }),
      ).subscribe({
        next: ({ reqs, pending }) => {
          this.requests = reqs.items.map(mapDocumentRequestDto);
          const pendingCount = pending.totalCount;
          const today = new Date().toISOString().slice(0, 10);
          const generatedToday = this.requests.filter(
            (r) => r.status === 'Generated' && r.requestDate.slice(0, 10) === today,
          ).length;
          this.stats = [
            {
              label: 'Total des demandes',
              value: reqs.totalCount,
              icon: 'history',
              color: 'text-blue-500',
              bg: 'bg-blue-500/10',
            },
            {
              label: 'Validations en attente',
              value: pendingCount,
              icon: 'clock',
              color: 'text-amber-500',
              bg: 'bg-amber-500/10',
            },
            {
              label: 'Générés aujourd’hui',
              value: generatedToday,
              icon: 'check-circle-2',
              color: 'text-emerald-500',
              bg: 'bg-emerald-500/10',
            },
          ];
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.requests = [];
          this.stats = [];
          this.loading = false;
          this.error = 'Impossible de charger les données RH.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  goHrMgmt(): void {
    this.nav.navigateToTab('hr-mgmt');
  }

  goTemplates(): void {
    this.nav.navigateToTab('templates');
  }

  pendingPreview(): DocumentationRequest[] {
    return this.requests.filter((r) => r.status === 'Pending').slice(0, 4);
  }
}
