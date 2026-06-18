import { Component, OnDestroy, OnInit, ViewEncapsulation, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  KyntusRoleDashboardComponent,
  KyntusDashboardAlertsComponent,
  KyntusDashboardRecentListComponent,
  KyntusActionInboxComponent,
  KyntusModuleHealthPanelComponent,
  type KyntusDashboardAlert,
  type KyntusKpiItem,
  type KyntusQuickAction,
} from '@/shared/components/ui';
import { AuthService } from '../../../../core/services/auth.service';
import { GlobalDashboardService } from '../../../../core/dashboard/global-dashboard.service';
import type { GlobalActionItem, ModuleHealthStatus } from '../../../../core/dashboard/global-dashboard.model';
import { KyntusNotificationHubService } from '../../../../core/notifications/kyntus-notification-hub.service';

@Component({
  selector: 'app-unified-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    KyntusRoleDashboardComponent,
    KyntusDashboardAlertsComponent,
    KyntusDashboardRecentListComponent,
    KyntusActionInboxComponent,
    KyntusModuleHealthPanelComponent,
  ],
  template: `
    <div class="ud-root ky-page-enter">
      <app-kyntus-role-dashboard
        [title]="dashboardTitle"
        [subtitle]="dashboardSubtitle"
        [greeting]="greetingLine"
        [roleBadge]="role"
        [loading]="false"
        [kpiLoading]="loadingKpis"
        [kpiItems]="kpiItems"
        [kpiColumns]="kpiColumns"
        [quickActions]="quickActions"
      >
        @if (!loadingKpis && alerts.length > 0) {
          <app-kyntus-dashboard-alerts dashboard-alerts [alerts]="alerts" />
        }

        @if (!loadingDetails) {
          <app-kyntus-action-inbox
            recentList
            title="File d'actions prioritaires"
            [items]="actionItems"
            viewAllRoute="/notifications"
            viewAllLabel="Centre notifications"
          />
        }

        @if (!loadingKpis && planningPreview) {
          <div charts class="ud-planning-preview">
            <h3>Ma semaine — {{ planningPreview.weekCode }}</h3>
            <div class="ud-planning-days">
              @for (day of planningPreview.days; track day.label) {
                <div class="ud-planning-day" [class.off]="day.off">
                  <span class="ud-planning-day-label">{{ day.label }}</span>
                  <span class="ud-planning-day-shift">{{ day.shift }}</span>
                </div>
              }
            </div>
          </div>
        }

        @if (!loadingDetails) {
          <app-kyntus-module-health-panel
            contextPanel
            title="Santé des modules"
            [items]="moduleHealth"
          />
        }

        @if (!loadingKpis) {
          <app-kyntus-dashboard-recent-list
            class="ud-recent-activity"
            title="Activité récente"
            [empty]="recentActivity().length === 0"
            emptyMessage="Aucune activité récente."
            viewAllRoute="/notifications"
            viewAllLabel="Voir tout"
          >
            <div rows>
              @for (item of recentActivity(); track item.id) {
                <div class="kyntus-recent-row">
                  <div class="kyntus-recent-row-main">
                    <p class="kyntus-recent-row-title">{{ item.title }}</p>
                    <p class="kyntus-recent-row-meta">{{ item.meta }}</p>
                  </div>
                  <div class="kyntus-recent-row-actions">
                    <button type="button" class="kyntus-recent-link" (click)="openActivity(item.id)">Ouvrir</button>
                  </div>
                </div>
              }
            </div>
          </app-kyntus-dashboard-recent-list>
        }
      </app-kyntus-role-dashboard>
    </div>
  `,
  styleUrls: ['./unified-dashboard.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class UnifiedDashboardComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly globalDashboard = inject(GlobalDashboardService);
  private readonly notifHub = inject(KyntusNotificationHubService);

  private sub = new Subscription();
  private loadStartedAt = 0;

  role = '';
  username = '';
  greetingLine = '';
  kpiColumns = 3;
  dashboardTitle = 'Tableau de bord';
  dashboardSubtitle = '';
  kpiItems: KyntusKpiItem[] = [];
  quickActions: KyntusQuickAction[] = [];
  alerts: KyntusDashboardAlert[] = [];
  actionItems: GlobalActionItem[] = [];
  moduleHealth: ModuleHealthStatus[] = [];
  loadingKpis = true;
  loadingDetails = true;
  planningPreview: { weekCode: string; days: { label: string; shift: string; off: boolean }[] } | null = null;

  readonly recentActivity = computed(() =>
    this.notifHub.notifications().slice(0, 5).map((n) => ({
      id: n.id,
      title: n.title,
      meta: `${n.body} · ${n.createdAt.toLocaleString('fr-FR')}`,
    })),
  );

  ngOnInit(): void {
    let user: { username?: string; role?: string } | null = null;
    try {
      user = JSON.parse(localStorage.getItem('user') || 'null');
    } catch {
      user = null;
    }
    this.username = user?.username || 'Utilisateur';
    this.role = (this.auth.getRole() || user?.role || '').trim();
    this.greetingLine = `Bonjour, ${this.username}`;
    this.loadSnapshot();
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  openActivity(id: string): void {
    const notif = this.notifHub.notifications().find((n) => n.id === id);
    if (notif) this.notifHub.openNotification(notif);
  }

  private loadSnapshot(): void {
    const authId = this.auth.getAuthUserId();
    this.loadingKpis = true;
    this.loadingDetails = true;
    this.loadStartedAt = performance.now();
    performance.mark('kyntus-dashboard-load-start');
    let emission = 0;
    this.sub.add(
      this.globalDashboard.loadSnapshot(this.role, authId).subscribe({
        next: (snapshot) => {
          emission += 1;
          const kpiMs = Math.round(performance.now() - this.loadStartedAt);
          if (emission === 1) {
            performance.mark('kyntus-dashboard-kpis-ready');
            performance.measure('kyntus-dashboard-kpis', 'kyntus-dashboard-load-start', 'kyntus-dashboard-kpis-ready');
            console.info(`[Kyntus /home] KPIs prêts en ${kpiMs} ms (rôle: ${this.role || 'inconnu'})`);
            this.loadingKpis = false;
          } else {
            console.info(`[Kyntus /home] Détails complets en ${kpiMs} ms`);
            this.loadingDetails = false;
          }
          this.dashboardTitle = snapshot.title;
          this.dashboardSubtitle = snapshot.subtitle;
          this.kpiItems = snapshot.kpis;
          this.kpiColumns = snapshot.kpis.length > 4 ? 3 : Math.min(snapshot.kpis.length, 4) || 3;
          this.alerts = snapshot.alerts;
          this.actionItems = snapshot.actionItems;
          this.moduleHealth = snapshot.moduleHealth;
          this.quickActions = snapshot.quickActions.map((qa) => ({
            label: qa.label,
            route: qa.route,
            action: qa.action,
          }));
          this.planningPreview = snapshot.planningPreview ?? null;
          if (emission === 1) {
            this.loadingDetails = false;
          }
        },
        error: () => {
          this.loadingKpis = false;
          this.loadingDetails = false;
        },
      }),
    );
  }
}
