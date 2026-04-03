import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display:flex; justify-content:center; align-items:center; height:100vh;">
      <p>Chargement en cours...</p>
    </div>
  `
})
export class AuthCallbackComponent implements OnInit {

  private readonly ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  private readonly ROLE_ROUTES: Record<string, string> = {
    'Admin':    '/dashboard',
    'Manager':  '/dashboard/manager',
    'Employee': '/dashboard/employee',
  };

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const token = this.route.snapshot.queryParams['token'];
    const refresh = this.route.snapshot.queryParams['refresh'];

    if (token) {
      localStorage.setItem('access_token', token);
      localStorage.setItem('refresh_token', refresh);

      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        const role = payload[this.ROLE_CLAIM] || '';

        console.log('Rôle détecté :', role); // ← pour vérifier dans F12

        localStorage.setItem('user', JSON.stringify({
          id: payload.sub || payload.nameid,
          username: payload.unique_name || payload.name || 'Utilisateur',
          email: payload.email || '',
          role: role
        }));

        const redirectTo = this.ROLE_ROUTES[role] ?? '/dashboard/employee';
        setTimeout(() => this.router.navigate([redirectTo]), 100);

      } catch (e) {
        console.warn('Impossible de décoder le token JWT', e);
        setTimeout(() => this.router.navigate(['/dashboard/employee']), 100);
      }

    } else {
      window.location.href = 'http://localhost:4201/login';
    }
  }
}