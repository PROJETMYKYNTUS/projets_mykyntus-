import { Component, ViewEncapsulation, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NotificationBellComponent } from '../../../contract/component/notification-bell/notification-bell.component';
import { NotificationService } from '../../../../core/services/notification.service'; // ← AJOUTEZ
import { JwtRoleService } from '../../../../core/auth/jwt-role.service';

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent],
  templateUrl: './dashboard-home.html',
  styleUrls: ['./dashboard-home.css'],
  encapsulation: ViewEncapsulation.None,
})
export class DashboardHomeComponent implements OnInit {

  currentUser: any = null;
  sidebarOpen = false;

  get userInitials(): string {
    const name: string = this.currentUser?.username || 'AD';
    return name.substring(0, 2).toUpperCase();
  }

  constructor(
    private router: Router,
    private notificationService: NotificationService, // ← AJOUTEZ
    private readonly jwtRole: JwtRoleService,
  ) {}

  /** Documentation RH : mêmes rôles JWT que le dashboard manager (`Admin`, `RH` — binôme). */
  get showDocumentationRhButton(): boolean {
    return this.jwtRole.hasDocumentationRhAccess();
  }


  ngOnInit(): void {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        this.currentUser = JSON.parse(userStr);
      } catch (e) {
        this.currentUser = null;
      }
    }

    const token = localStorage.getItem('token');
    if (!token) {
      window.location.href = 'http://localhost:4201/login';
    }

    // ← AJOUTEZ — connecter le hub reclamation pour les managers
    if (this.currentUser?.id) {
      this.notificationService.connectAsManager(this.currentUser.id);
    }
  }

  logout(): void {
    this.notificationService.disconnect(); // ← AJOUTEZ
    localStorage.clear();
    window.location.href = 'http://localhost:4201/login';
  }
}