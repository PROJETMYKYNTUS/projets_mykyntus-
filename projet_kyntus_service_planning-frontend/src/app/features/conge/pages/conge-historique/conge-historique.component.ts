import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { Calendar, RefreshCw, Search } from 'lucide';
import type { User } from '../../../users/users-module';
import type { Department } from '../../../prime/models';
import type { OperationalDepartmentNode, OrgPoleNode } from '../../../prime/models/org-tree.types';
import {
  DemandeCongeDto,
  StatutDemande,
  StatutDemandeLabels,
  TypeCongeLabels,
  TypeCongeExceptionnelLabels,
  TypeConge,
} from '../../../../core/models/conge.models';
import { buildOperationalOrgFilterOptions } from '../../../../core/org/org-structure-filter';
import {
  enrichUserOrgPerimeter,
  orgPerimeterSummary,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';

@Component({
  selector: 'app-conge-historique',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective, KyntusPageHeaderComponent],
  templateUrl: './conge-historique.component.html',
  styleUrls: ['./conge-historique.component.css'],
})
export class CongeHistoriqueComponent implements OnInit {
  readonly icons = { search: Search, calendar: Calendar, refresh: RefreshCw };
  readonly statutLabels = StatutDemandeLabels;
  readonly typeCongeLabels = TypeCongeLabels;
  readonly TypeConge = TypeConge;
  readonly StatutDemande = StatutDemande;

  loading = false;
  error: string | null = null;

  allDemandes: DemandeCongeDto[] = [];
  filteredDemandes: DemandeCongeDto[] = [];

  filterAnnee = new Date().getFullYear();
  yearOptions: number[] = [];
  filterStatut: StatutDemande | '' = '';
  filterOperationalDepartment = '';
  filterPole = '';
  filterCellule = '';
  filterService = '';
  searchTerm = '';
  /** Deep-link depuis la recherche globale. */
  highlightDemandeId: string | null = null;

  operationalDepartmentOptions: string[] = [];
  poleOptions: string[] = [];
  celluleOptions: string[] = [];
  serviceOptions: string[] = [];
  operationalDepartments: OperationalDepartmentNode[] = [];
  unassignedPoles: OrgPoleNode[] = [];
  legacyDepartments: Department[] = [];
  private perimeterByEmployeGuid = new Map<string, UserOrgPerimeterView>();

  readonly orgPerimeterSummary = orgPerimeterSummary;

  private readonly route = inject(ActivatedRoute);

  constructor(
    private congeService: CongeService,
    private userService: UserService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const current = new Date().getFullYear();
    this.yearOptions = Array.from({ length: 6 }, (_, i) => current - i);

    const qp = this.route.snapshot.queryParamMap;
    const annee = Number(qp.get('annee'));
    if (Number.isFinite(annee) && annee >= 2000) {
      this.filterAnnee = annee;
    }
    this.highlightDemandeId = qp.get('demandeId')?.trim() || null;

    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.error = null;
    forkJoin({
      demandes: this.congeService.getHistoriqueRh(this.filterAnnee),
      users: this.userService.getAllUsers(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      overview: this.orgApi.loadOverview(),
      subServices: this.subServiceService.getAllSubServices(),
    }).subscribe({
      next: ({ demandes, users, departments, overview, subServices }) => {
        this.allDemandes = demandes ?? [];
        this.operationalDepartments = overview.operationalDepartments ?? [];
        this.unassignedPoles = overview.unassignedPoles ?? [];
        this.legacyDepartments = departments?.length ? departments : (overview.departments ?? []);
        this.buildPerimeters(users ?? [], overview, subServices ?? []);
        this.refreshOrgFilterOptions();
        this.applyFilters();
        this.focusHighlightedDemande();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger l\'historique des congés.';
        this.cdr.detectChanges();
      },
    });
  }

  private focusHighlightedDemande(): void {
    const id = this.highlightDemandeId;
    if (!id) return;
    const found = this.allDemandes.find((d) => d.id === id);
    if (!found) return;
    this.filteredDemandes = [found, ...this.filteredDemandes.filter((d) => d.id !== id)];
    setTimeout(() => {
      const el = document.querySelector(`[data-demande-id="${CSS.escape(id)}"]`);
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 80);
  }

  private buildPerimeters(
    users: User[],
    overview: Parameters<typeof enrichUserOrgPerimeter>[2],
    subServices: Parameters<typeof enrichUserOrgPerimeter>[3],
  ): void {
    this.perimeterByEmployeGuid.clear();
    for (const u of users) {
      const guid = (u.guid ?? '').trim().toLowerCase();
      if (!guid) continue;
      this.perimeterByEmployeGuid.set(
        guid,
        enrichUserOrgPerimeter(u, this.legacyDepartments, overview, subServices),
      );
    }
  }

  employePerimeter(employeId: string): UserOrgPerimeterView {
    return this.perimeterByEmployeGuid.get(employeId.trim().toLowerCase())
      ?? { operationalDepartment: null, pole: null, cellule: null, service: null };
  }

  refreshOrgFilterOptions(): void {
    const opts = buildOperationalOrgFilterOptions(this.operationalDepartments, {
      operationalDepartment: this.filterOperationalDepartment || undefined,
      pole: this.filterPole || undefined,
      cellule: this.filterCellule || undefined,
    });
    this.operationalDepartmentOptions = opts.operationalDepartments;
    this.poleOptions = opts.poles;
    this.celluleOptions = opts.cellules;
    this.serviceOptions = opts.services;
  }

  patchFilterAnnee(annee: string): void {
    this.filterAnnee = Number(annee) || new Date().getFullYear();
    this.loadData();
  }

  patchFilterOperationalDepartment(dept: string): void {
    this.filterOperationalDepartment = dept;
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  patchFilterPole(pole: string): void {
    this.filterPole = pole;
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  patchFilterCellule(cellule: string): void {
    this.filterCellule = cellule;
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  patchFilterService(service: string): void {
    this.filterService = service;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.filterStatut = '';
    this.filterOperationalDepartment = '';
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  applyFilters(): void {
    let rows = [...this.allDemandes];
    const term = this.searchTerm.trim().toLowerCase();
    if (term) {
      rows = rows.filter((d) => {
        const name = this.employeDisplayName(d).toLowerCase();
        const p = this.employePerimeter(d.employeId);
        const hay = [name, d.motif, p.operationalDepartment, p.pole, p.cellule, p.service].filter(Boolean).join(' ').toLowerCase();
        return hay.includes(term);
      });
    }
    if (this.filterStatut !== '') {
      rows = rows.filter((d) => d.statut === +this.filterStatut);
    }
    if (this.filterOperationalDepartment) {
      rows = rows.filter((d) => this.employePerimeter(d.employeId).operationalDepartment === this.filterOperationalDepartment);
    }
    if (this.filterPole) {
      rows = rows.filter((d) => this.employePerimeter(d.employeId).pole === this.filterPole);
    }
    if (this.filterCellule) {
      rows = rows.filter((d) => this.employePerimeter(d.employeId).cellule === this.filterCellule);
    }
    if (this.filterService) {
      rows = rows.filter((d) => this.employePerimeter(d.employeId).service === this.filterService);
    }
    this.filteredDemandes = rows;
    this.cdr.detectChanges();
  }

  decideurLabel(d: DemandeCongeDto): string {
    return (d.superviseurDecideurNom || d.rhDecideurNom || '').trim() || '—';
  }

  employeDisplayName(d: DemandeCongeDto): string {
    const nom = [d.prenomEmploye, d.nomEmploye].filter(Boolean).join(' ').trim();
    return nom || d.employeId;
  }

  typeLabel(d: DemandeCongeDto): string {
    if (d.typeConge === TypeConge.Exceptionnel && d.typeExceptionnel != null) {
      return TypeCongeExceptionnelLabels[d.typeExceptionnel] ?? this.typeCongeLabels[d.typeConge];
    }
    return this.typeCongeLabels[d.typeConge] ?? String(d.typeConge);
  }

  statutClass(statut: StatutDemande): string {
    const map: Partial<Record<StatutDemande, string>> = {
      [StatutDemande.EnAttente]: 's-pending',
      [StatutDemande.EnAttenteRh]: 's-pending',
      [StatutDemande.Validee]: 's-valid',
      [StatutDemande.Refusee]: 's-refused',
      [StatutDemande.Annulee]: 's-cancel',
    };
    return map[statut] ?? '';
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '—';
    return new Date(value).toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  get statsTotal(): number { return this.filteredDemandes.length; }
  get statsValidees(): number {
    return this.filteredDemandes.filter((d) => d.statut === StatutDemande.Validee).length;
  }
  get statsRefusees(): number {
    return this.filteredDemandes.filter((d) => d.statut === StatutDemande.Refusee).length;
  }
  get statsJours(): number {
    return this.filteredDemandes
      .filter((d) => d.statut === StatutDemande.Validee)
      .reduce((sum, d) => sum + (d.nombreJours ?? 0), 0);
  }
}
