import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PlanningService,
  PlanningWeekItem,
  PlanningWeekList,
  AutoGenerateSettings,
} from '../../services/planning.service';
import { UserService } from '../../../users/services/user.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { Calendar, ChevronLeft, ChevronRight, RefreshCw, Settings } from 'lucide';

@Component({
  selector: 'app-planning-validation',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusPageHeaderComponent],
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

  showSettings = false;
  settings: AutoGenerateSettings = {
    enabled: true,
    dayOfWeek: 4,
    hourLocal: 6,
    minuteLocal: 0,
    timeZone: 'Africa/Casablanca',
    target: 'NextWeek',
  };

  readonly dayNames = ['Dimanche', 'Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi'];

  private planningUserId: number | null = null;

  constructor(
    private planningService: PlanningService,
    private userService: UserService,
    private session: KyntusSessionService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.initNextWeek();
    this.userService.getCurrentUser().subscribe({
      next: (u) => {
        this.planningUserId = u?.id ?? null;
        this.loadWeek();
        this.loadSettings();
      },
      error: () => {
        this.loadWeek();
        this.loadSettings();
      },
    });
  }

  initNextWeek(): void {
    const today = new Date();
    const monday = this.getMondayOfWeek(today);
    monday.setDate(monday.getDate() + 7);
    this.weekStartDate = this.formatDate(monday);
    this.weekCode = this.getWeekCode(monday);
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
    this.router.navigate(['/planning/view', item.planningId], {
      queryParams: { from: 'validation', weekCode: this.weekCode },
    });
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
