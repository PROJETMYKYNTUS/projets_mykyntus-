import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FloorService } from '../../services/floor.service';
import { Floor } from '../../models/floor.model';
import { NavigationActionsService } from '../../../../core/navigation/navigation-actions.service';

@Component({
  selector: 'app-floor-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './floor-list.component.html',
  styleUrls: ['./floor-list.component.css']
})
export class FloorListComponent implements OnInit {
  floors: Floor[] = [];
  loading = false;
  error: string | null = null;

  constructor(
    private floorService: FloorService,
    private navActions: NavigationActionsService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadFloors();
  }

  loadFloors(): void {
    this.loading = true;
    this.error = null;
    this.floorService.getAllFloors().subscribe({
      next: (floors: Floor[]) => {
        this.floors = floors;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  goToOrganisationRh(): void {
    void this.navActions.openOrganisationRh('departments');
  }
}
