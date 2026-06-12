import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  Calendar,
  Check,
  Clock,
  GraduationCap,
  Inbox,
  Loader2,
  Search,
  User,
} from 'lucide';
import { FormationService } from '../../../core/services/formation.service';
import {
  FormationDto,
  StatutFormation,
  StatutFormationLabels,
} from '../../../core/models/formation.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

@Component({
  selector: 'app-formation-employee',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './formation-employee.component.html',
  styleUrls: ['./formation-employee.component.css'],
})
export class FormationEmployeeComponent implements OnInit {
  readonly icons = {
    graduation: GraduationCap,
    check: Check,
    search: Search,
    inbox: Inbox,
    loader: Loader2,
    user: User,
    calendar: Calendar,
    clock: Clock,
  };

  formations: FormationDto[] = [];
  filteredFormations: FormationDto[] = [];
  loading = false;
  searchTerm = '';
  toast = { show: false, message: '', type: 'success' as 'success' | 'error' };
  statutLabels = StatutFormationLabels;
  StatutFormation = StatutFormation;

  userId = '';
  userName = '';
  inscritFormations = new Set<string>();

  constructor(
    private svc: FormationService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    this.userName = user?.username || 'Employé';

    const rawId = user?.id;
    if (typeof rawId === 'string' && rawId.includes('-')) {
      this.userId = rawId;
    } else {
      const padded = String(rawId).padStart(12, '0');
      this.userId = `00000000-0000-0000-0000-${padded}`;
    }

    this.loadFormations();
  }

  get stats() {
    const list = this.formations;
    const placesRestantes = list.reduce(
      (sum, f) => sum + Math.max(0, f.capaciteMax - f.nombreInscrits),
      0,
    );
    return {
      disponibles: list.length,
      inscrit: this.inscritFormations.size,
      places: placesRestantes,
    };
  }

  loadFormations(): void {
    this.loading = true;
    this.cdr.detectChanges();
    this.svc.getAll(StatutFormation.Validee).subscribe({
      next: (data) => {
        this.formations = data;
        this.applyFilters();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  applyFilters(): void {
    const q = this.searchTerm.trim().toLowerCase();
    this.filteredFormations = q
      ? this.formations.filter(
          (f) =>
            f.titre.toLowerCase().includes(q) ||
            f.formateur.toLowerCase().includes(q) ||
            f.description.toLowerCase().includes(q),
        )
      : [...this.formations];
    this.cdr.detectChanges();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.applyFilters();
  }

  inscrire(f: FormationDto): void {
    if (!this.userId) {
      this.showToast('Utilisateur non identifié.', 'error');
      return;
    }
    if (this.inscritFormations.has(f.id)) return;

    this.svc
      .inscrire(f.id, {
        formationId: f.id,
        employeId: this.userId,
        nomEmploye: this.userName,
      })
      .subscribe({
        next: () => {
          this.inscritFormations.add(f.id);
          this.showToast('Inscription réussie !', 'success');
          this.loadFormations();
        },
        error: (err) => {
          const msg =
            err?.error?.errors?.['$.employeId']?.[0] ||
            err?.error?.error ||
            "Erreur lors de l'inscription.";
          this.showToast(msg, 'error');
        },
      });
  }

  estComplet(f: FormationDto): boolean {
    return f.nombreInscrits >= f.capaciteMax;
  }

  estInscrit(f: FormationDto): boolean {
    return this.inscritFormations.has(f.id);
  }

  getStatutClass(statut: StatutFormation): string {
    const map: Record<number, string> = {
      0: 'badge-draft',
      1: 'badge-pending',
      2: 'badge-valid',
      3: 'badge-active',
      4: 'badge-done',
      5: 'badge-cancel',
    };
    return map[statut] || '';
  }

  showToast(message: string, type: 'success' | 'error'): void {
    this.toast = { show: true, message, type };
    this.cdr.detectChanges();
    setTimeout(() => {
      this.toast.show = false;
      this.cdr.detectChanges();
    }, 4000);
  }
}
