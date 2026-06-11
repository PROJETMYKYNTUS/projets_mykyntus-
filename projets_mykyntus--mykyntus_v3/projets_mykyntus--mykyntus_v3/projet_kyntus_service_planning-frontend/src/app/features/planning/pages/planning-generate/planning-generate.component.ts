// features/planning/pages/planning-generator/planning-generator.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PlanningService,
  SubServiceSimple,
  WeeklyPlanningResponse
} from '../../services/planning.service';

@Component({
  selector: 'app-planning-generate',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './planning-generate.component.html',
  styleUrls: ['./planning-generate.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class PlanningGenerateComponent implements OnInit {

  // ── Formulaire ──
  subServiceId  = 0;
  weekCode      = '';
  weekStartDate = '';
  totalEffectif = 0;

  // ── State ──
  subServices:   SubServiceSimple[] = [];
  plannings:     WeeklyPlanningResponse[] = [];
  loading        = false;
  generating     = false;
  error          = '';
  successMsg     = '';
   Math          = Math;

  // ── Semaine courante ──
  currentWeekCode   = '';
  currentWeekStart  = '';

  constructor(
    private planningService: PlanningService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.initCurrentWeek();
    this.loadSubServices();
  }

  // ── Initialiser avec la semaine courante ──
  initCurrentWeek(): void {
    const today  = new Date();
    const monday = this.getMondayOfWeek(today);
    this.weekStartDate  = this.formatDate(monday);
    this.currentWeekStart = this.weekStartDate;
    this.weekCode       = this.getWeekCode(monday);
    this.currentWeekCode = this.weekCode;
  }

  // ── Charger sous-services ──
  loadSubServices(): void {
    this.planningService.getSubServices().subscribe({
      next: data => {
        this.subServices = data;
        if (data.length > 0) {
          this.subServiceId = data[0].id;
          this.loadPlannings();
        }
        this.cdr.detectChanges();
      },
      error: () => { this.cdr.detectChanges(); }
    });
  }

  // ── Charger plannings existants ──
  loadPlannings(): void {
    if (!this.subServiceId) return;
    this.loading = true;
    this.cdr.detectChanges();

    this.planningService.getBySubService(this.subServiceId).subscribe({
      next: data => {
        this.plannings = data;
        this.loading   = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  onSubServiceChange(): void { this.loadPlannings(); }

  onWeekChange(): void {
    if (this.weekStartDate) {
      const d = new Date(this.weekStartDate);
      this.weekCode = this.getWeekCode(d);
    }
  }

  // ── Créer et générer le planning ──
  generatePlanning(): void {
    if (!this.subServiceId || !this.weekCode || !this.weekStartDate || !this.totalEffectif) {
      this.error = 'Veuillez remplir tous les champs.';
      return;
    }

    this.generating = true;
    this.error      = '';
    this.successMsg = '';
    this.cdr.detectChanges();

    // Étape 1 : Créer le planning
    this.planningService.create({
      subServiceId:  this.subServiceId,
      weekCode:      this.weekCode,
      weekStartDate: this.weekStartDate,
      totalEffectif: this.totalEffectif
    }).subscribe({
      next: planning => {
        // Étape 2 : Générer automatiquement
        this.planningService.generate({
          weeklyPlanningId: planning.id,
          totalEffectif:    this.totalEffectif
        }).subscribe({
          next: result => {
            this.generating = false;
            this.successMsg = `Planning ${result.weekCode} généré avec succès !`;
            this.loadPlannings();
            this.cdr.detectChanges();
            // Rediriger vers la vue
            setTimeout(() => this.router.navigate(['/planning/view', result.id]), 1500);
          },
          error: err => {
            this.generating = false;
            this.error = `Erreur génération : ${err.error?.message ?? 'Erreur serveur'}`;
            this.cdr.detectChanges();
          }
        });
      },
      error: err => {
        this.generating = false;
        this.error = err.status === 409
          ? `Planning ${this.weekCode} existe déjà pour ce sous-service.`
          : `Erreur création : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  viewPlanning(id: number): void {
    this.router.navigate(['/planning/view', id]);
  }

  goBack(): void { this.router.navigate(['/dashboard']); }
deletePlanning(id: number, event: Event): void {
  event.stopPropagation(); // empêcher navigation vers la vue
  if (!confirm('Supprimer ce planning ? Cette action est irréversible.')) return;

  this.planningService.delete(id).subscribe({
    next: () => {
      this.plannings = this.plannings.filter(p => p.id !== id);
      this.cdr.detectChanges();
    },
    error: err => {
      this.error = `Erreur suppression : ${err.error?.message ?? 'Erreur serveur'}`;
      this.cdr.detectChanges();
    }
  });
}
  // ── Helpers ──
  getMondayOfWeek(date: Date): Date {
    const d   = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    d.setDate(diff);
    return d;
  }

  getWeekCode(monday: Date): string {
    const year    = monday.getFullYear();
    const weekNum = this.getISOWeek(monday);
    return `${year}-W${weekNum.toString().padStart(2, '0')}`;
  }

  getISOWeek(date: Date): number {
    const d = new Date(date);
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 3 - (d.getDay() + 6) % 7);
    const week1 = new Date(d.getFullYear(), 0, 4);
    return 1 + Math.round(((d.getTime() - week1.getTime()) / 86400000 - 3 +
      (week1.getDay() + 6) % 7) / 7);
  }

  formatDate(d: Date): string {
    return d.toISOString().substring(0, 10);
  }

  getStatusClass(status: string): string {
    return ({ Draft: 'st-draft', Published: 'st-published', Archived: 'st-archived' } as any)[status] ?? '';
  }

  getStatusLabel(status: string): string {
    return ({ Draft: 'Brouillon', Published: 'Publié', Archived: 'Archivé' } as any)[status] ?? status;
  }
}