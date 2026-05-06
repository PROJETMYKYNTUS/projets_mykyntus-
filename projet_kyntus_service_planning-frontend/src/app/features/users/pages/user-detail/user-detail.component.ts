import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './user-detail.component.html',
  styleUrls: ['./user-detail.component.css']
})
export class UserDetailComponent implements OnInit {
  user: User | null = null;
  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private userService: UserService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadUser(id);
  }

  loadUser(id: number): void {
    this.loading = true;
    this.error = null;
    this.userService.getUserById(id).subscribe({
      next: (user) => { this.user = user; this.loading = false; this.cdr.detectChanges(); },
      error: (err) => { this.error = `Erreur: ${err.status}`; this.loading = false; this.cdr.detectChanges(); }
    });
  }

  getAnciennete(hireDate: string): string {
    const debut = new Date(hireDate);
    const now = new Date();
    const totalMois = (now.getFullYear() - debut.getFullYear()) * 12
                    + (now.getMonth() - debut.getMonth());
    const ans = Math.floor(totalMois / 12);
    const mois = totalMois % 12;

    if (totalMois <= 0) return "Moins d'1 mois";
    if (ans === 0) return `${mois} mois`;
    if (mois === 0) return `${ans} an${ans > 1 ? 's' : ''}`;
    return `${ans} an${ans > 1 ? 's' : ''} et ${mois} mois`;
  }

  goBack(): void { this.router.navigate(['/users']); }
  editUser(): void { this.router.navigate(['/users', 'edit', this.user?.id]); }

  deleteUser(): void {
    if (!this.user) return;
    if (confirm('Supprimer cet employé ?')) {
      this.userService.deleteUser(this.user.id).subscribe({
        next: () => this.router.navigate(['/users']),
        error: (err) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}