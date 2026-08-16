import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FloorService } from '../../services/floor.service';
import { Floor } from '../../models/floor.model';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';

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
    private confirmService: KyntusConfirmService,
    private toastService: KyntusToastService,
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

  async deleteFloor(): Promise<void> {
    if (!this.floor) return;
    const ok = await this.confirmService.confirm({
      title: 'Supprimer l\'étage',
      message: 'Supprimer cet étage ?',
      confirmLabel: 'Supprimer',
      variant: 'danger',
    });
    if (!ok) return;
    this.floorService.deleteFloor(this.floor.id).subscribe({
      next: () => this.router.navigate(['/floors']),
      error: (err: any) => this.toastService.error(`Erreur: ${err.error?.message}`),
    });
  }
}