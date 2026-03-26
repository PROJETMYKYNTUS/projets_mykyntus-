import { Component, ViewEncapsulation, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NotificationBellComponent } from '../../../contract/component/notification-bell/notification-bell.component';

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

  constructor(private router: Router) {}

  ngOnInit(): void {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        this.currentUser = JSON.parse(userStr);
      } catch (e) {
        this.currentUser = null;
      }
    }

    const token = localStorage.getItem('access_token');
    if (!token) {
      window.location.href = 'http://localhost:4201/login';
    }
  }

logout(): void {
  localStorage.clear();
  window.location.href = 'http://localhost:4201/login';
}
}