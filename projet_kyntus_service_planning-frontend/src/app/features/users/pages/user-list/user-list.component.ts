import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.css']
})
export class UserListComponent implements OnInit {
  users: User[] = [];
  filteredUsers: User[] = [];
  searchTerm = '';
  loading = false;
  error: string | null = null;

  constructor(
    private userService: UserService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void { this.loadUsers(); }

  loadUsers(): void {
    this.loading = true;
    this.error = null;
    this.userService.getAllUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.filteredUsers = users;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSearch(): void {
    const term = this.searchTerm.toLowerCase().trim();
    this.filteredUsers = term
      ? this.users.filter(u =>
          `${u.firstName} ${u.lastName}`.toLowerCase().includes(term) ||
          `${u.lastName} ${u.firstName}`.toLowerCase().includes(term)
        )
      : this.users;
  }

  getAnciennete(hireDate: string): string {
  const debut = new Date(hireDate);
  const now = new Date();

  // ── Calcul des années et mois ──
  let ans   = now.getFullYear() - debut.getFullYear();
  let mois  = now.getMonth()    - debut.getMonth();
  let jours = now.getDate()     - debut.getDate();

  // Ajustement des jours négatifs
  if (jours < 0) {
    mois--;
    // Récupère le nombre de jours du mois précédent
    const dernierMois = new Date(now.getFullYear(), now.getMonth(), 0);
    jours += dernierMois.getDate();
  }

  // Ajustement des mois négatifs
  if (mois < 0) {
    ans--;
    mois += 12;
  }

  // ── Formatage ──
  const totalJours = Math.floor((now.getTime() - debut.getTime()) / (1000 * 60 * 60 * 24));

  if (totalJours <= 0) return "Pas encore embauché";
  if (totalJours < 30) return `${totalJours} jour${totalJours > 1 ? 's' : ''}`;

  const partAns  = ans  > 0 ? `${ans} an${ans > 1 ? 's' : ''}`     : '';
  const partMois = mois > 0 ? `${mois} mois`                        : '';
  const partJour = jours > 0 ? `${jours} jour${jours > 1 ? 's' : ''}` : '';

  return [partAns, partMois, partJour].filter(Boolean).join(' et ');
}

  goToDashboard(): void { this.router.navigate(['/dashboard']); }
  goimport() : void {this.router.navigate(['/users/import']);}
  viewUser(id: number): void { this.router.navigate(['/users', id]); }
  editUser(id: number): void { this.router.navigate(['/users', 'edit', id]); }
  createUser(): void { this.router.navigate(['/users', 'create']); }

  deleteUser(id: number): void {
    if (confirm('Supprimer cet employé ?')) {
      this.userService.deleteUser(id).subscribe({
        next: () => this.loadUsers(),
        error: (err) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}