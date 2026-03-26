import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {

  constructor(private router: Router) {}

  canActivate(): boolean {
    const token = localStorage.getItem('access_token');

    if (token) {
      return true;
    }

    // ✅ Rediriger vers l'auth-frontend, pas vers /login
    window.location.href = 'http://localhost:4201/login';
    return false;
  }
}