import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard-admin',
  standalone: true,
  imports: [CommonModule],
  template: `<div style="padding:2rem"><h1>Dashboard Admin</h1></div>`
})
export class DashboardAdminComponent {}