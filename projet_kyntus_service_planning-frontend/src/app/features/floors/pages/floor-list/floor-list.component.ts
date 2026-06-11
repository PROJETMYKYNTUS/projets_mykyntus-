import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FloorService } from '../../services/floor.service';
import { Floor } from '../../models/floor.model';

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
    private router: Router,
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

  goToDashboard(): void { this.router.navigate(['/dashboard']); }
  viewFloor(id: number): void { this.router.navigate(['/floors', id]); }
  editFloor(id: number): void { this.router.navigate(['/floors', 'edit', id]); }
  createFloor(): void { this.router.navigate(['/floors', 'create']); }

  deleteFloor(id: number): void {
    if (confirm('Supprimer cet étage ?')) {
      this.floorService.deleteFloor(id).subscribe({
        next: () => this.loadFloors(),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}