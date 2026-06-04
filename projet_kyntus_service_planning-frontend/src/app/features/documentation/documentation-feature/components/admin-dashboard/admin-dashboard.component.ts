import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { forkJoin, Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import { switchMapOnDocumentationContext } from '../../lib/documentation-context-refresh';
import { DocumentationApiService } from '../../services/documentation-api.service';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, DocIconComponent],
  templateUrl: './admin-dashboard.component.html',
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  loading = true;
  error: string | null = null;
  stats: Array<{
    label: string;
    value: number | string;
    icon: string;
    color: string;
    bg: string;
  }> = [];

  private sub = new Subscription();

  constructor(
    private readonly nav: DocumentationNavigationService,
    private readonly api: DocumentationApiService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  ngOnInit(): void {
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () =>
        forkJoin({
          requests: this.api.getDocumentRequestsPage(1),
          types: this.api.getDocTypesForCatalog(),
          audit: this.api.getAuditLogsPage(1),
        }),
      ).subscribe({
        next: ({ requests, types, audit }) => {
          this.stats = [
            {
              label: 'Demandes (total)',
              value: requests.totalCount,
              icon: 'history',
              color: 'text-blue-500',
              bg: 'bg-blue-500/10',
            },
            {
              label: 'Types catalogue',
              value: types.length,
              icon: 'layers',
              color: 'text-amber-500',
              bg: 'bg-amber-500/10',
            },
            {
              label: 'Événements audit',
              value: audit.totalCount,
              icon: 'activity',
              color: 'text-emerald-500',
              bg: 'bg-emerald-500/10',
            },
          ];
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.stats = [];
          this.loading = false;
          this.error = 'Impossible de charger les indicateurs administrateur.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  go(tab: 'doc-types' | 'permissions' | 'workflow' | 'storage'): void {
    this.nav.navigateToTab(tab);
  }
}
