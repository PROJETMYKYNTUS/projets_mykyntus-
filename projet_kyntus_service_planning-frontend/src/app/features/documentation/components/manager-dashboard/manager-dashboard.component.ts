import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { DocumentationRole } from '../../interfaces/documentation-role';

@Component({
  selector: 'app-manager-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './manager-dashboard.component.html',
})
export class ManagerDashboardComponent {
  @Input() role!: DocumentationRole;
}
