import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { inject } from '@angular/core';
import { Component, ViewEncapsulation, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { redirectToAuthLogin } from '../../../../core/session/kyntus-auth-refresh.service';

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard-home.html',
  styleUrls: ['./dashboard-home.css'],
  encapsulation: ViewEncapsulation.None,
})
export class DashboardHomeComponent implements OnInit {
  private readonly session = inject(KyntusSessionService);

  currentUser: any = null;
  sidebarOpen = false;

  get userInitials(): string {
    const name: string = this.currentUser?.username || 'AD';
    return name.substring(0, 2).toUpperCase();
  }

  constructor(private router: Router) {}

  ngOnInit(): void {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        this.currentUser = JSON.parse(userStr);
      } catch {
        this.currentUser = null;
      }
    }

    const token = localStorage.getItem('token');
    if (!token) {
      redirectToAuthLogin();
    }
  }

  get congesRoute(): string {
    const role: string = this.currentUser?.role || '';
    const managerRoles = ['Admin', 'RH', 'Manager'];
    return managerRoles.includes(role) ? '/conges/validation' : '/mes-conges';
  }

  get congesLabel(): string {
    const role: string = this.currentUser?.role || '';
    return ['Admin', 'RH', 'Manager'].includes(role)
      ? 'Validation des congés'
      : 'Mes congés';
  }

  logout(): void {
    redirectToAuthLogin(undefined, { clearReturnUrl: true });
  }

  openDocumentationRhApp(): void {
    const email = (this.session.getEmail() || this.currentUser?.email as string | undefined)?.trim();
    const queryParams: Record<string, string> = { handoff: 'rh' };
    if (email) {
      queryParams['email'] = email;
    }
    void this.router.navigate(['/documentation', 'hr-mgmt'], { queryParams });
  }
}
