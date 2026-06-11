import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FloorService } from '../../services/floor.service';

@Component({
  selector: 'app-floor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './floor-form.component.html',
  styleUrls: ['./floor-form.component.css']
})
export class FloorFormComponent implements OnInit {
  form!: FormGroup;
  isEditMode = false;
  floorId: number | null = null;
  loading = false;
  submitting = false;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private floorService: FloorService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      floorNumber: [null, [Validators.required, Validators.min(0)]],
      name:        ['',   [Validators.required, Validators.minLength(2)]],
      description: ['']
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.floorId = Number(id);
      setTimeout(() => this.loadFloor(this.floorId!), 0);  // ← setTimeout
    }
  }

  loadFloor(id: number): void {
    this.loading = true;
    this.floorService.getFloorById(id).subscribe({
      next: (floor: any) => {
        this.form.patchValue({
          floorNumber: floor.floorNumber,
          name:        floor.name,
          description: floor.description
        });
        this.loading = false;
      },
      error: (err: any) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting = true;
    const dto = this.form.value;

    const request$ = this.isEditMode && this.floorId
      ? this.floorService.updateFloor(this.floorId, dto)
      : this.floorService.createFloor(dto);

    request$.subscribe({
      next: () => {
        this.submitting = false;
        this.router.navigate(['/floors']);
      },
      error: (err: any) => {
        this.error = `Erreur: ${err.error?.message || err.message}`;
        this.submitting = false;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/floors']);
  }

  get floorNumber() { return this.form.get('floorNumber'); }
  get name()        { return this.form.get('name'); }
}