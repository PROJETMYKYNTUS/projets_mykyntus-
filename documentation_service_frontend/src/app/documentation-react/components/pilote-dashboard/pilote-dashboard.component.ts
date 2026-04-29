import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { forkJoin, Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import type { DocumentationRequest } from '../../interfaces/documentation-entities';
import { switchMapOnDocumentationContext } from '../../lib/documentation-context-refresh';
import { mapDocumentRequestDto } from '../../lib/documentation-dto-mappers';
import { DocumentationApiService } from '../../services/documentation-api.service';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-pilote-dashboard',
  standalone: true,
  imports: [CommonModule, DocIconComponent, StatusBadgeComponent],
  templateUrl: './pilote-dashboard.component.html',
  styleUrl: './pilote-dashboard.component.scss',
})
export class PiloteDashboardComponent implements OnInit, OnDestroy {
  private readonly role = 'Pilote' as const;
  private sub = new Subscription();

  loading = true;
  error: string | null = null;
  private scopedReqs: DocumentationRequest[] = [];
  private myDocsCount = 0;

  stats: Array<{
    label: string;
    value: number;
    icon: string;
    color: string;
    bg: string;
  }> = [];

  recentRows: DocumentationRequest[] = [];

  constructor(
    private readonly api: DocumentationApiService,
    private readonly nav: DocumentationNavigationService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  ngOnInit(): void {
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () =>
        forkJoin({
          submitted: this.api.getAllMyDocumentRequests(),
          assigned: this.api.getAllAssignedDocumentRequests(),
        }),
      ).subscribe({
        next: ({ submitted, assigned }) => {
          const submittedUi = submitted.map(mapDocumentRequestDto);
          const assignedUi = assigned.map(mapDocumentRequestDto);
          this.scopedReqs = submittedUi;
          const mergedByInternal = new Map<string, DocumentationRequest>();
          for (const r of [...submittedUi, ...assignedUi]) {
            mergedByInternal.set(r.internalId, r);
          }
          this.myDocsCount = [...mergedByInternal.values()].filter((r) => r.status === 'Generated').length;
          const active = submittedUi.filter((r) => r.status === 'Pending').length;
          const approved = submittedUi.filter((r) => r.status === 'Approved').length;
          const total = submittedUi.length;
          this.stats = [
            {
              label: 'Mes documents',
              value: this.myDocsCount,
              icon: 'file-text',
              color: 'text-blue-500',
              bg: 'bg-blue-500/10',
            },
            {
              label: 'Demandes actives',
              value: active,
              icon: 'clock',
              color: 'text-amber-500',
              bg: 'bg-amber-500/10',
            },
            {
              label: 'Approuvées',
              value: approved,
              icon: 'check-circle-2',
              color: 'text-emerald-500',
              bg: 'bg-emerald-500/10',
            },
            {
              label: 'Total des demandes',
              value: total,
              icon: 'history',
              color: 'text-indigo-500',
              bg: 'bg-indigo-500/10',
            },
          ];
          this.recentRows = [...submittedUi]
            .sort((a, b) => b.requestDate.localeCompare(a.requestDate))
            .slice(0, 4);
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.scopedReqs = [];
          this.stats = [];
          this.recentRows = [];
          this.loading = false;
          this.error = 'Impossible de charger les demandes. Vérifiez l’API et la base de données.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  goRequest(): void {
    this.nav.navigateToTab('request');
  }

  goMyDocs(): void {
    this.nav.navigateToTab('my-docs');
  }

  goTracking(): void {
    this.nav.navigateToTab('tracking');
  }
}
