import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  PlanningService,
  PlanningWeekItem,
  PlanningWeekList,
  AutoGenerateSettings,
  PendingRequestsSummary,
} from '../../services/planning.service';
import { UserService } from '../../../users/services/user.service';
import type { User } from '../../../users/users-module';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { Calendar, ChevronLeft, ChevronRight, RefreshCw, Settings } from 'lucide';
import { RouterLink } from '@angular/router';
import { KyntusRoleNames } from '../../../../core/org/kyntus-role-names';

interface AgentOption {
  id: number;
  label: string;
}

@Component({
  selector: 'app-planning-validation',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusPageHeaderComponent, RouterLink],
  templateUrl: './planning-validation.component.html',
  styleUrls: ['./planning-validation.component.css'],
})
export class PlanningValidationComponent implements OnInit {
  readonly icons = {
    calendar: Calendar,
    prev: ChevronLeft,
    next: ChevronRight,
    refresh: RefreshCw,
    settings: Settings,
  };

  weekCode = '';
  weekStartDate = '';
  list: PlanningWeekList | null = null;
  loading = false;
  generating = false;
  savingSettings = false;
  error = '';
  successMsg = '';
  statusFilter: 'all' | 'draft' | 'published' | 'missing' = 'all';
  search = '';
  agentFilterId: number | null = null;
  agentSearch = '';
  private usersById = new Map<number, User>();

  showSettings = false;
  settings: AutoGenerateSettings = {
    enabled: true,
    dayOfWeek: 4,
    hourLocal: 6,
    minuteLocal: 0,
    timeZone: 'Africa/Casablanca',
    target: 'NextWeek',
  };

  pendingSummary: PendingRequestsSummary | null = null;

  readonly dayNames = ['Dimanche', 'Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi'];

  private planningUserId: number | null = null;
  private authUserId: number | null = null;

  constructor(
    private planningService: PlanningService,
    private userService: UserService,
    private session: KyntusSessionService,
    private router: Router,
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.initWeekFromRouteOrDefault();
    this.authUserId = this.session.getAuthUserId() ?? null;
    this.userService.getAllUsers().subscribe({
      next: (users) => {
        this.usersById = new Map(users.filter((u) => u.isActive).map((u) => [u.id, u]));
        this.cdr.detectChanges();
      },
    });
    this.userService.getCurrentUser().subscribe({
      next: (u) => {
        this.planningUserId = u?.id ?? null;
        this.loadWeek();
        this.loadSettings();
        this.loadPendingSummary();
      },
      error: () => {
        this.loadWeek();
        this.loadSettings();
        this.loadPendingSummary();
      },
    });
  }

  get canSeePendingBanner(): boolean {
    const role = this.session.getRole() ?? '';
    return role === KyntusRoleNames.Admin || role === KyntusRoleNames.RH;
  }

  loadPendingSummary(): void {
    if (!this.canSeePendingBanner) {
      this.pendingSummary = null;
      return;
    }
    this.planningService.getPendingRequestsSummary(this.authUserId ?? undefined).subscribe({
      next: (s) => {
        this.pendingSummary = s;
        this.cdr.detectChanges();
      },
      error: () => {
        this.pendingSummary = null;
      },
    });
  }

  /** Restaure la semaine via query params (retour depuis la vue détail), sinon semaine prochaine. */
  initWeekFromRouteOrDefault(): void {
    const qp = this.route.snapshot.queryParamMap;
    const weekCode = qp.get('weekCode');
    const weekStart = qp.get('weekStart');

    if (weekCode && weekStart) {
      this.weekCode = weekCode;
      this.weekStartDate = weekStart.split('T')[0];
      return;
    }

    if (weekCode) {
      const monday = this.mondayFromIsoWeek(weekCode);
      if (monday) {
        this.weekCode = weekCode;
        this.weekStartDate = this.formatDate(monday);
        return;
      }
    }

    this.initNextWeek();
  }

  initNextWeek(): void {
    const today = new Date();
    const monday = this.getMondayOfWeek(today);
    monday.setDate(monday.getDate() + 7);
    this.weekStartDate = this.formatDate(monday);
    this.weekCode = this.getWeekCode(monday);
  }

  /** Convertit un code ISO `YYYY-Www` en lundi de la semaine. */
  private mondayFromIsoWeek(weekCode: string): Date | null {
    const match = /^(\d{4})-W(\d{1,2})$/i.exec(weekCode.trim());
    if (!match) return null;
    const year = Number(match[1]);
    const week = Number(match[2]);
    if (!Number.isFinite(year) || !Number.isFinite(week) || week < 1 || week > 53) {
      return null;
    }
    const jan4 = new Date(year, 0, 4);
    const day = jan4.getDay() || 7;
    const mondayWeek1 = new Date(jan4);
    mondayWeek1.setDate(jan4.getDate() - day + 1);
    const monday = new Date(mondayWeek1);
    monday.setDate(mondayWeek1.getDate() + (week - 1) * 7);
    monday.setHours(0, 0, 0, 0);
    return monday;
  }

  loadWeek(): void {
    this.loading = true;
    this.error = '';
    this.cdr.detectChanges();
    this.planningService.getWeekOverview(this.weekCode, this.planningUserId ?? undefined).subscribe({
      next: (data) => {
        this.list = data;
        this.weekStartDate = data.weekStartDate?.toString?.() ?? this.weekStartDate;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err.error?.message ?? 'Impossible de charger la semaine.';
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  loadSettings(): void {
    this.planningService.getAutoGenerateSettings().subscribe({
      next: (s) => {
        this.settings = s;
        this.cdr.detectChanges();
      },
    });
  }

  saveSettings(): void {
    this.savingSettings = true;
    this.planningService
      .saveAutoGenerateSettings(this.settings, this.planningUserId ?? undefined)
      .subscribe({
        next: (s) => {
          this.settings = s;
          this.savingSettings = false;
          this.successMsg = 'Paramètres de génération enregistrés.';
          this.showSettings = false;
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.savingSettings = false;
          this.error = err.error?.message ?? 'Erreur sauvegarde paramètres.';
          this.cdr.detectChanges();
        },
      });
  }

  prevWeek(): void {
    const d = this.parseDate(this.weekStartDate);
    d.setDate(d.getDate() - 7);
    this.applyWeek(d);
  }

  nextWeek(): void {
    const d = this.parseDate(this.weekStartDate);
    d.setDate(d.getDate() + 7);
    this.applyWeek(d);
  }

  private applyWeek(monday: Date): void {
    const m = this.getMondayOfWeek(monday);
    this.weekStartDate = this.formatDate(m);
    this.weekCode = this.getWeekCode(m);
    this.loadWeek();
  }

  regenerate(): void {
    this.generating = true;
    this.error = '';
    this.successMsg = '';
    this.planningService.autoGenerateWeek(this.weekCode, false).subscribe({
      next: (r) => {
        this.generating = false;
        this.successMsg = `Génération : ${r.created} créé(s), ${r.skipped} ignoré(s), ${r.errors} erreur(s).`;
        this.loadWeek();
        this.loadPendingSummary();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.generating = false;
        this.error = err.error?.message ?? 'Erreur génération.';
        this.cdr.detectChanges();
      },
    });
  }

  openItem(item: PlanningWeekItem): void {
    if (!item.planningId) return;
    const qp: Record<string, string | number> = {
      from: 'validation',
      weekCode: this.weekCode,
    };
    if (this.agentFilterId != null) {
      qp['highlightUserId'] = this.agentFilterId;
    }
    this.router.navigate(['/planning/view', item.planningId], { queryParams: qp });
  }

  get agentOptions(): AgentOption[] {
    const ids = new Set<number>();
    for (const item of this.list?.items ?? []) {
      for (const uid of item.assignedUserIds ?? []) ids.add(uid);
    }
    const q = this.agentSearch.trim().toLowerCase();
    return [...ids]
      .map((id) => {
        const u = this.usersById.get(id);
        const label = u ? `${u.firstName} ${u.lastName}`.trim() : `Agent #${id}`;
        return { id, label };
      })
      .filter((o) => !q || o.label.toLowerCase().includes(q))
      .sort((a, b) => a.label.localeCompare(b.label, 'fr'));
  }

  clearAgentFilter(): void {
    this.agentFilterId = null;
    this.agentSearch = '';
  }

  get filteredItems(): PlanningWeekItem[] {
    let items = this.list?.items ?? [];
    const q = this.search.trim().toLowerCase();
    if (q) {
      items = items.filter(
        (i) =>
          i.orgLabel.toLowerCase().includes(q) ||
          i.subServiceName.toLowerCase().includes(q),
      );
    }
    if (this.agentFilterId != null) {
      const agentId = this.agentFilterId;
      items = items.filter((i) => (i.assignedUserIds ?? []).includes(agentId));
    }
    switch (this.statusFilter) {
      case 'draft':
        return items.filter((i) => i.status === 'Draft');
      case 'published':
        return items.filter((i) => i.status === 'Published');
      case 'missing':
        return items.filter((i) => !i.planningId);
      default:
        return items;
    }
  }

  statusLabel(item: PlanningWeekItem): string {
    if (!item.hasTemplate) return 'Config manquante';
    if (!item.planningId) return 'Non généré';
    if (item.status === 'Draft') return 'À valider';
    if (item.status === 'Published') return 'Validé';
    return item.status ?? '—';
  }

  statusClass(item: PlanningWeekItem): string {
    if (!item.hasTemplate) return 'st-missing-cfg';
    if (!item.planningId) return 'st-missing';
    if (item.status === 'Draft') return 'st-draft';
    if (item.status === 'Published') return 'st-published';
    return '';
  }

  weekRangeLabel(): string {
    const start = this.parseDate(this.weekStartDate);
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    const fmt = (d: Date) =>
      d.toLocaleDateString('fr-FR', { day: '2-digit', month: 'short' });
    return `${fmt(start)} – ${fmt(end)}`;
  }

  private parseDate(value: string): Date {
    if (!value) return this.getMondayOfWeek(new Date());
    const [y, m, d] = value.split('T')[0].split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  getMondayOfWeek(date: Date): Date {
    const d = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    d.setDate(diff);
    d.setHours(0, 0, 0, 0);
    return d;
  }

  getWeekCode(monday: Date): string {
    const weekNum = this.getISOWeek(monday);
    const year = monday.getFullYear();
    return `${year}-W${weekNum.toString().padStart(2, '0')}`;
  }

  getISOWeek(date: Date): number {
    const d = new Date(date);
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7));
    const week1 = new Date(d.getFullYear(), 0, 4);
    return (
      1 +
      Math.round(
        ((d.getTime() - week1.getTime()) / 86400000 - 3 + ((week1.getDay() + 6) % 7)) / 7,
      )
    );
  }

  formatDate(d: Date): string {
    return d.toLocaleDateString('en-CA');
  }

  canValidateRole(): boolean {
    const role = this.session.getRole();
    return role === 'Admin' || role === 'RH';
  }
}
