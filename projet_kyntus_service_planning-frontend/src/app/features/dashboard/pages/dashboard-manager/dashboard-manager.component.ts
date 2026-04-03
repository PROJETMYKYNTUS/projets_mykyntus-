import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard-manager',
  standalone: true,
  imports: [CommonModule],
  template: `<div style="padding:2rem"><h1>Dashboard Manager</h1></div>`
})
export class DashboardManagerComponent {}