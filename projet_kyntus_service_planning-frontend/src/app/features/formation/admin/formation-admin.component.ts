import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormationService } from '../../../core/services/formation.service';
import {
  FormationDto, CreateFormationCommand,
  StatutFormation, StatutFormationLabels
} from '../../../core/models/formation.models';

@Component({
  selector: 'app-formation-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './formation-admin.component.html',
  styleUrls: ['./formation-admin.component.css']
})
export class FormationAdminComponent implements OnInit {

  formations: FormationDto[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  selectedId = '';
  statutLabels = StatutFormationLabels;
  StatutFormation = StatutFormation;

  form: CreateFormationCommand = {
    titre: '', description: '', formateur: '',
    dateDebut: '', dateFin: '', capaciteMax: 10, prix: 0
  };

  toast = { show: false, message: '', type: 'success' };

  constructor(
    private svc: FormationService,
  private cdr: ChangeDetectorRef
) {}

  ngOnInit(): void { this.loadFormations(); }

loadFormations(): void {
  this.loading = true;
  this.cdr.detectChanges();
  this.svc.getAll().subscribe({
    next: (data) => {
      this.formations = data;
      this.loading = false;
      this.cdr.detectChanges(); // ← ajouter
    },
    error: () => {
      this.loading = false;
      this.cdr.detectChanges();
    }
  });
}

  openCreate(): void {
    this.editMode = false;
    this.selectedId = '';
    this.form = {
      titre: '', description: '', formateur: '',
      dateDebut: '', dateFin: '', capaciteMax: 10, prix: 0
    };
    this.showModal = true;
  }

  openEdit(f: FormationDto): void {
    this.editMode = true;
    this.selectedId = f.id;
    this.form = {
      titre: f.titre, description: f.description, formateur: f.formateur,
      dateDebut: f.dateDebut.substring(0, 10),
      dateFin: f.dateFin.substring(0, 10),
      capaciteMax: f.capaciteMax, prix: f.prix
    };
    this.showModal = true;
  }

  submit(): void {
  const cmd = {
    ...this.form,
    dateDebut: this.form.dateDebut + 'T00:00:00Z',
    dateFin: this.form.dateFin + 'T00:00:00Z'
  };

  if (this.editMode) {
    // ← séparer les deux appels
    this.svc.update(this.selectedId, cmd).subscribe({
      next: () => {
        this.showModal = false;
        this.loadFormations();
        this.showToast('Formation mise à jour !', 'success');
      },
      error: () => this.showToast('Erreur lors de la sauvegarde.', 'error')
    });
  } else {
    this.svc.create(cmd).subscribe({
      next: () => {
        this.showModal = false;
        this.loadFormations();
        this.showToast('Formation créée !', 'success');
      },
      error: () => this.showToast('Erreur lors de la sauvegarde.', 'error')
    });
  }
}

  valider(f: FormationDto): void {
    this.svc.valider(f.id).subscribe({
      next: () => { this.loadFormations(); this.showToast('Formation validée !', 'success'); },
      error: () => this.showToast('Impossible de valider.', 'error')
    });
  }

  delete(f: FormationDto): void {
    if (!confirm(`Supprimer "${f.titre}" ?`)) return;
    this.svc.delete(f.id).subscribe({
      next: () => { this.loadFormations(); this.showToast('Formation supprimée.', 'success'); },
      error: () => this.showToast('Erreur lors de la suppression.', 'error')
    });
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
    setTimeout(() => this.toast.show = false, 4000);
  }
}