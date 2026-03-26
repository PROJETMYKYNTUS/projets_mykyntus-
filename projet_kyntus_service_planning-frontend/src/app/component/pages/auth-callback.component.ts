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

  constructor(
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.queryParams['token'];
    const refresh = this.route.snapshot.queryParams['refresh'];

    if (token) {
      // 1. Stocker les tokens
      localStorage.setItem('access_token', token);
      localStorage.setItem('refresh_token', refresh);

      // 2. Décoder le token pour extraire le user
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        const user = {
          id: payload.sub || payload.nameid,
          username: payload.unique_name || payload.name || 'Utilisateur',
          email: payload.email || ''
        };
        localStorage.setItem('user', JSON.stringify(user));
      } catch (e) {
        // Si décodage échoue, on continue quand même
        console.warn('Impossible de décoder le token JWT', e);
      }

      // 3. Attendre que localStorage soit écrit avant de naviguer
      setTimeout(() => {
        this.router.navigate(['/dashboard']);
      }, 100);

    } else {
      window.location.href = 'http://localhost:4201/login';
    }
  }
}