import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { RegisterComponent } from './components/register/register';
import { DashboardComponent } from './components/dashboard/dashboard';
import { authGuard } from './guards/auth-guard';


export const routes: Routes = [
  // Route par défaut - redirection vers login
  { 
    path: '', 
    redirectTo: '/login', 
    pathMatch: 'full' 
  },
  
  // Routes publiques (avec noAuthGuard pour rediriger si déjà connecté)
  { 
    path: 'login', 
    component: LoginComponent,

  },
  { 
    path: 'register', 
    component: RegisterComponent,
    
  },
  
  // Routes protégées (avec authGuard)
  
  // Route 404 - redirection vers login
  { 
    path: '**', 
    redirectTo: '/login' 
  }
];