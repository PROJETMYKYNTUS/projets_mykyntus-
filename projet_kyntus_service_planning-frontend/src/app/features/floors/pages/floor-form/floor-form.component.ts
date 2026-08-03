import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FloorService } from '../../services/floor.service';
import { KyntusFormDraftDirective } from '../../../../core/drafts/kyntus-form-draft.directive';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';

@Component({
  selector: 'app-floor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, KyntusFormDraftDirective],
  templateUrl: './floor-form.component.html',
  styleUrls: ['./floor-form.component.css']
})
export class FloorFormComponent implements OnInit {
  @ViewChild(KyntusFormDraftDirective) private draftDir?: KyntusFormDraftDirective;

  form!: FormGroup;
  isEditMode = false;
  floorId: number | null = null;
  loading = false;
  submitting = false;
  error: string | null = null;
  draftKey = 'floor-form-create';

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private floorService: FloorService,
    private readonly formDrafts: KyntusFormDraftService,
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
      this.draftKey = `floor-form-edit-${id}`;
      setTimeout(() => this.loadFloor(this.floorId!), 0);
    }
  }

  loadFloor(id: number): void {
    this.loading = true;
    this.floorService.getFloorById(id).subscribe({
      next: (floor: any) => {
        const draft = this.formDrafts.load<Record<string, unknown>>(this.draftKey);
        this.form.patchValue({
          floorNumber: floor.floorNumber,
          name:        floor.name,
          description: floor.description
        });
        if (draft && typeof draft === 'object') {
          this.form.patchValue(draft);
          this.form.markAsDirty();
        }
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
        this.draftDir?.markSaved();
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