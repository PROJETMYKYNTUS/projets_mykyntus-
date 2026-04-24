import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  errorMessage: string = '';
  loading: boolean = false;
  showPassword: boolean = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // ✅ Nettoyer TOUS les tokens au chargement du login
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('currentUser');
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    localStorage.removeItem('user');
    localStorage.removeItem('token_type');

    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    this.authService.loading$.subscribe((loading: boolean) => {
      this.loading = loading;
    });

    // ✅ Supprimé — plus de redirection auto
  }

  onSubmit(): void {
    this.errorMessage = '';

    if (this.loginForm.invalid) {
      this.markFormGroupTouched(this.loginForm);
      return;
    }

    this.loading = true;
    const loginData = this.loginForm.value;

    this.authService.login(loginData).subscribe({
     next: (response: any) => {
  console.log('Connexion réussie', response);

  // ✅ Extraire userId depuis le token JWT
  const payload = JSON.parse(atob(response.accessToken.split('.')[1]));
  const userId = parseInt(
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
  );

  // Stocker les tokens
  localStorage.setItem('access_token', response.accessToken);
  localStorage.setItem('refresh_token', response.refreshToken);
  localStorage.setItem('token_type', response.tokenType);

  // ✅ Stocker user AVEC l'id
  localStorage.setItem('user', JSON.stringify({
    id: userId,
    username: response.user?.username || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'],
    email: response.user?.email || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
    role: response.user?.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  }));

  // Rediriger vers planning
  window.location.href =
    `http://localhost:4200/auth-callback?token=${response.accessToken}&refresh=${response.refreshToken}`;
},
      error: (error: any) => {
        console.error('Erreur de connexion', error);
        this.loading = false;

        if (error.status === 401) {
          this.errorMessage = 'Email ou mot de passe incorrect';
        } else if (error.status === 0) {
          this.errorMessage = 'Impossible de contacter le serveur';
        } else {
          this.errorMessage = error.error?.message || 'Une erreur est survenue';
        }
      }
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);
      control?.markAsTouched();
    });
  }

  hasError(fieldName: string, errorType: string): boolean {
    const field = this.loginForm.get(fieldName);
    return !!(field?.hasError(errorType) && field?.touched);
  }
}