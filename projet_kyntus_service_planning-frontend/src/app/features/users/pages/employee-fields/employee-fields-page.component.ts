import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowLeft, AlertTriangle, Plus, Save, Trash2 } from 'lucide';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { formatHttpErrorMessage } from '../../../../core/lib/http-error-message.util';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../../core/drafts/kyntus-object-draft.binder';
import {
  CreateEmployeeFieldRequest,
  EmployeeFieldService,
  UpdateEmployeeFieldRequest,
} from '../../services/employee-field.service';
import { EmployeeImportFieldConfig } from '../../services/employee-import.service';
import {
  applyFieldLockToPayload,
  isEnabledCheckboxLocked,
  isRequiredCheckboxLocked,
  lockHint,
} from '../../utils/employee-field-locks.util';

@Component({
  selector: 'app-employee-fields-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './employee-fields-page.component.html',
  styleUrls: ['./employee-fields-page.component.css'],
})
export class EmployeeFieldsPageComponent implements OnInit, OnDestroy {
  readonly icons = { back: ArrowLeft, plus: Plus, save: Save, trash: Trash2, warn: AlertTriangle };
  readonly isEnabledCheckboxLocked = isEnabledCheckboxLocked;
  readonly isRequiredCheckboxLocked = isRequiredCheckboxLocked;
  readonly lockHint = lockHint;
  private readonly formDrafts = inject(KyntusFormDraftService);
  private draftBinder?: KyntusObjectDraftBinder<{
    showCreate: boolean;
    newField: CreateEmployeeFieldRequest;
    aliasesInput: string;
  }>;

  /** Champs système retirés du modèle (import legacy) — masqués dans l'admin. */
  private static readonly hiddenSystemFieldKeys = new Set([
    'isNewEmployee',
    'managerEmail',
    'structurePole',
    'structureCellule',
    'structureService',
    'subService',
    'chefDeProjetEmail',
    'superviseurEmail',
    'referentTechniqueEmail',
  ]);

  private readonly fieldSvc = inject(EmployeeFieldService);
  private readonly cdr = inject(ChangeDetectorRef);

  fields: EmployeeImportFieldConfig[] = [];
  loading = false;
  saving = false;
  error: string | null = null;
  showCreate = false;

  readonly dataTypes = [
    { value: 'text', label: 'Texte' },
    { value: 'date', label: 'Date' },
    { value: 'number', label: 'Nombre' },
    { value: 'boolean', label: 'Oui / Non' },
  ];

  newField: CreateEmployeeFieldRequest = {
    label: '',
    dataType: 'text',
    isRequiredOnCreate: false,
    isEnabled: true,
    aliases: [],
  };

  aliasesInput = '';

  ngOnInit(): void {
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'employee-fields-create',
      () => ({
        showCreate: this.showCreate,
        newField: { ...this.newField },
        aliasesInput: this.aliasesInput,
      }),
      (s) => {
        if (s.newField) this.newField = { ...this.newField, ...s.newField };
        if (typeof s.aliasesInput === 'string') this.aliasesInput = s.aliasesInput;
        if (s.showCreate && s.newField?.label) this.showCreate = true;
      },
    );
    this.draftBinder.start();
    this.loadFields();
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
  }

  touchDraft(): void {
    this.draftBinder?.touch();
  }

  loadFields(): void {
    this.loading = true;
    this.error = null;
    this.fieldSvc.getFields().subscribe({
      next: (fields) => {
        this.fields = fields.map((f) => {
          const locked = applyFieldLockToPayload(f);
          return { ...f, isEnabled: locked.isEnabled, isRequiredOnCreate: locked.isRequiredOnCreate };
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Impossible de charger les champs employés.');
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  typeLabel(dataType?: string): string {
    return this.dataTypes.find((t) => t.value === dataType)?.label ?? 'Texte';
  }

  openCreate(): void {
    this.showCreate = true;
    this.error = null;
    this.newField = {
      label: '',
      dataType: 'text',
      isRequiredOnCreate: false,
      isEnabled: true,
      aliases: [],
    };
    this.aliasesInput = '';
  }

  createField(): void {
    if (!this.newField.label.trim()) {
      this.error = 'Le libellé est obligatoire.';
      return;
    }
    this.saving = true;
    this.error = null;
    const request: CreateEmployeeFieldRequest = {
      ...this.newField,
      label: this.newField.label.trim(),
      aliases: this.parseAliases(this.aliasesInput),
    };
    this.fieldSvc.createField(request).subscribe({
      next: () => {
        this.showCreate = false;
        this.saving = false;
        this.draftBinder?.clear();
        this.loadFields();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Création impossible.');
        this.saving = false;
        this.cdr.detectChanges();
      },
    });
  }

  saveField(field: EmployeeImportFieldConfig): void {
    const locked = applyFieldLockToPayload(field);
    field.isEnabled = locked.isEnabled;
    field.isRequiredOnCreate = locked.isRequiredOnCreate;
    const request: UpdateEmployeeFieldRequest = {
      label: field.label,
      dataType: field.dataType ?? 'text',
      isRequiredOnCreate: locked.isRequiredOnCreate,
      isEnabled: locked.isEnabled,
      sortOrder: field.sortOrder,
      aliases: field.aliases ?? [],
    };
    this.saving = true;
    this.error = null;
    this.fieldSvc.updateField(field.fieldKey, request).subscribe({
      next: () => {
        this.saving = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Enregistrement impossible.');
        this.saving = false;
        this.cdr.detectChanges();
      },
    });
  }

  deleteField(field: EmployeeImportFieldConfig): void {
    if (field.isSystemField) return;
    if (!confirm(`Supprimer le champ « ${field.label} » et toutes ses valeurs ?`)) return;
    this.fieldSvc.deleteField(field.fieldKey).subscribe({
      next: () => this.loadFields(),
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Suppression impossible.');
        this.cdr.detectChanges();
      },
    });
  }

  customFields(): EmployeeImportFieldConfig[] {
    return this.fields.filter((f) => !f.isSystemField);
  }

  systemFields(): EmployeeImportFieldConfig[] {
    return this.fields.filter(
      (f) => f.isSystemField && !EmployeeFieldsPageComponent.hiddenSystemFieldKeys.has(f.fieldKey),
    );
  }

  private parseAliases(raw: string): string[] {
    return raw
      .split(',')
      .map((a) => a.trim())
      .filter((a) => a.length > 0);
  }
}
