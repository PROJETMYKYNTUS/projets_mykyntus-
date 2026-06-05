import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormationService } from '../../../core/services/formation.service';
import { FormationDto, StatutFormation, StatutFormationLabels } from '../../../core/models/formation.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { Check } from 'lucide';

@Component({
  selector: 'app-formation-employee',
  standalone: true,
  imports: [CommonModule, LucideIconComponent],
  templateUrl: './formation-employee.component.html',
  styleUrls: ['./formation-employee.component.css']
})
export class FormationEmployeeComponent implements OnInit {
  readonly icons = { check: Check };

  formations: FormationDto[] = [];
  loading = false;
  toast = { show: false, message: '', type: 'success' };
  statutLabels = StatutFormationLabels;
  StatutFormation = StatutFormation;

  userId = '';
  userName = '';
inscritFormations = new Set<string>(); 
  constructor(private svc: FormationService,
   private cdr: ChangeDetectorRef            
  ) {}

 ngOnInit(): void {
  const user = JSON.parse(localStorage.getItem('user') || '{}');
  this.userName = user?.username || 'Employé';
  
  // Convertir l'id en Guid
  const rawId = user?.id;
  if (typeof rawId === 'string' && rawId.includes('-')) {
    this.userId = rawId; // déjà un Guid
  } else {
    // Construire un Guid à partir de l'entier
    const padded = String(rawId).padStart(12, '0');
    this.userId = `00000000-0000-0000-0000-${padded}`;
  }
  
  this.loadFormations();
}


  loadFormations(): void {
    this.loading = true;
    this.cdr.detectChanges(); // ← ajouter
    this.svc.getAll(StatutFormation.Validee).subscribe({
      next: (data) => {
        this.formations = data;
        this.loading = false;
        this.cdr.detectChanges(); // ← ajouter
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges(); // ← ajouter
      }
    });
  }

inscrire(f: FormationDto): void {
  if (!this.userId) {
    this.showToast("Utilisateur non identifié.", 'error');
    return;
  }
if (this.inscritFormations.has(f.id)) return;
  this.svc.inscrire(f.id, {
    formationId: f.id,
    employeId: this.userId,   // ← maintenant c'est un Guid
    nomEmploye: this.userName
  }).subscribe({
    next: () => {
        this.inscritFormations.add(f.id);
      this.showToast('Inscription réussie !', 'success');
      this.loadFormations();
    },
    error: (err) => {
      const msg = err?.error?.errors?.['$.employeId']?.[0]
                || err?.error?.error
                || "Erreur lors de l'inscription.";
      this.showToast(msg, 'error');
    }
  });
}
  estComplet(f: FormationDto): boolean {
    return f.nombreInscrits >= f.capaciteMax;
  }
    estInscrit(f: FormationDto): boolean { // ← 3. AJOUTER
    return this.inscritFormations.has(f.id);
  }

  getStatutClass(statut: StatutFormation): string {
    const map: Record<number, string> = {
      0: 'badge-draft', 1: 'badge-pending', 2: 'badge-valid',
      3: 'badge-active', 4: 'badge-done', 5: 'badge-cancel'
    };
    return map[statut] || '';
  }

showToast(message: string, type: 'success' | 'error'): void {
    this.toast = { show: true, message, type };
    this.cdr.detectChanges(); // ← ajouter
    setTimeout(() => {
      this.toast.show = false;
      this.cdr.detectChanges(); // ← ajouter
    }, 4000);
  }
}