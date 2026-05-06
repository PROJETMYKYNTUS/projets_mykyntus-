// features/contract/pages/contract-form/contract-form.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ContractService, CreateContractDto, UpdateContractDto } from '../../services/contract.service';
import { UserService } from '../../../users/services/user.service';

interface User { id: number; firstName: string; lastName: string; }

@Component({
  selector: 'app-contract-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './contract-form.component.html',
  styleUrls: ['./contract-form.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class ContractFormComponent implements OnInit {

  contractForm!: FormGroup;
  isEditMode = false;
  editingId: number | null = null;
  saving = false;
  users: User[] = [];

  contractTypes = [
    { value: 'CDI',    label: 'CDI',    icon: '📋', desc: 'Durée indéterminée' },
    { value: 'CDD',    label: 'CDD',    icon: '📅', desc: 'Durée déterminée'   },
    { value: 'Stage',  label: 'Stage',  icon: '🎓', desc: 'Stage de formation' },
    { value: 'ANAPEC',label: 'ANAPEC',icon: '🔄', desc: 'Mission temporaire' },
  ];

  contractStatuses = [
    { label: "En période d'essai", value: 0 },
    { label: 'Actif',              value: 1 },
    { label: 'Expiré',             value: 2 },
    { label: 'Résilié',            value: 3 },
  ];

  defaultProbation: Record<string, number> = {
    CDI: 90, CDD: 30, Stage: 15, Interim: 0
  };

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private contractService: ContractService,
    private userService: UserService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.buildForm();
    this.loadUsers();

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEditMode = true;
      this.editingId  = +id;

      // ✅ FIX — supprimer les validators qui bloquent en mode édition
      this.contractForm.get('userId')?.clearValidators();
      this.contractForm.get('userId')?.updateValueAndValidity();

      this.contractForm.get('startDate')?.clearValidators();
      this.contractForm.get('startDate')?.updateValueAndValidity();

      this.contractForm.get('endDate')?.clearValidators();
      this.contractForm.get('endDate')?.updateValueAndValidity();

      this.loadContract(+id);
    }

    // Réactivité type → endDate required (création seulement)
    this.contractForm.get('type')?.valueChanges.subscribe(type => {
      if (!this.isEditMode) {
        const endCtrl = this.contractForm.get('endDate');
        if (type !== 'CDI') {
          endCtrl?.setValidators(Validators.required);
        } else {
          endCtrl?.clearValidators();
          endCtrl?.setValue('');
        }
        endCtrl?.updateValueAndValidity();
      }
      this.cdr.detectChanges();
    });
  }

  buildForm(): void {
    this.contractForm = this.fb.group({
      userId:             ['', Validators.required],
      type:               ['CDI', Validators.required],
      startDate:          ['', Validators.required],
      endDate:            [''],
      probationDays:      [null],
      alertThresholdDays: [15],
      status:             [0],
      notes:              ['']
    });
  }

  get f() { return this.contractForm.controls; }

  selectType(value: string): void {
    this.contractForm.patchValue({ type: value });
  }

  loadUsers(): void {
    this.userService.getAllUsers().subscribe({
      next: (data: any[]) => {
        this.users = data.map(u => ({
          id:        u.id,
          firstName: u.firstName ?? u.prenom ?? '',
          lastName:  u.lastName  ?? u.nom    ?? ''
        }));
        this.cdr.detectChanges();
      },
      error: err => console.error('Erreur chargement utilisateurs:', err)
    });
  }

  loadContract(id: number): void {
    this.contractService.getById(id).subscribe({
      next: c => {
        const statusValue = this.contractStatuses.find(s => s.label === c.status)?.value ?? 0;
        this.contractForm.patchValue({
          type:               c.type,
          startDate:          c.startDate?.substring(0, 10) ?? '',
          endDate:            c.endDate?.substring(0, 10)   ?? '',
          alertThresholdDays: c.alertThresholdDays,
          status:             statusValue,
          notes:              c.notes ?? ''
        });
        this.cdr.detectChanges();
      },
      error: err => console.error('Erreur chargement contrat:', err)
    });
  }

  statusLabelToValue(label: string): number {
    return this.contractStatuses.find(s => s.label === label)?.value ?? 0;
  }

  onSubmit(): void {
    if (this.contractForm.invalid) {
      this.contractForm.markAllAsTouched();
      this.cdr.detectChanges();
      return;
    }

    this.saving = true;

    if (this.isEditMode && this.editingId) {
      const dto: UpdateContractDto = {
        status: this.f['status'].value !== null ? this.f['status'].value : undefined,
      };

      const type = this.f['type'].value;
      if (type) dto.type = type;

      const endDate = this.f['endDate'].value;
      if (endDate && endDate !== '') dto.endDate = endDate;

      const probationDays = this.f['probationDays'].value;
      if (probationDays && probationDays > 0) dto.probationDays = probationDays;

      const alertDays = this.f['alertThresholdDays'].value;
      if (alertDays && alertDays > 0) dto.alertThresholdDays = alertDays;

      const notes = this.f['notes'].value;
      if (notes !== null && notes !== undefined) dto.notes = notes;

      this.contractService.update(this.editingId, dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur mise à jour:', err); this.saving = false; this.cdr.detectChanges(); }
      });

    } else {
      const dto: CreateContractDto = {
        userId:             +this.f['userId'].value,
        type:               this.f['type'].value,
        startDate:          this.f['startDate'].value,
        endDate:            this.f['endDate'].value || undefined,
        probationDays:      this.f['probationDays'].value || undefined,
        alertThresholdDays: this.f['alertThresholdDays'].value,
        notes:              this.f['notes'].value
      };

      this.contractService.create(dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur création:', err); this.saving = false; this.cdr.detectChanges(); }
      });
    }
  }

  getDefaultProbationValue(): number {
    const type = this.contractForm?.get('type')?.value ?? 'CDI';
    return this.defaultProbation[type] ?? 0;
  }

  getDefaultProbationLabel(): string {
    return `Par défaut : ${this.getDefaultProbationValue()} jours`;
  }

  goBack(): void { this.router.navigate(['/contracts']); }
}