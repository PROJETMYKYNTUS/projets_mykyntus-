import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FloorService } from '../../services/floor.service';
import { Floor } from '../../models/floor.model';

@Component({
  selector: 'app-floor-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './floor-detail.component.html',
  styleUrls: ['./floor-detail.component.css']
})
export class FloorDetailComponent implements OnInit {
  floor: Floor | null = null;
  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private floorService: FloorService,
    private cdr: ChangeDetectorRef  // ← ajouter
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadFloor(id);
  }

  loadFloor(id: number): void {
    this.loading = true;
    this.error = null;
    this.floorService.getFloorById(id).subscribe({
      next: (floor: Floor) => {
        this.floor = floor;
        this.loading = false;
        this.cdr.detectChanges();  // ← ajouter
      },
      error: (err: any) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();  // ← ajouter
      }
    });
  }

  editFloor(): void {
    this.router.navigate(['/floors', 'edit', this.floor?.id]);
  }

  goBack(): void {
    this.router.navigate(['/floors']);
  }

  deleteFloor(): void {
    if (!this.floor) return;
    if (confirm('Supprimer cet étage ?')) {
      this.floorService.deleteFloor(this.floor.id).subscribe({
        next: () => this.router.navigate(['/floors']),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}