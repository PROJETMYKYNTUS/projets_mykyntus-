import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LucideIconComponent } from '../../lucide-icon.component';
import type { IconNode } from 'lucide';
import { ClipboardList, Calendar, GraduationCap, RefreshCw, FileText } from 'lucide';
import type { ContractType } from '../../../features/contract/services/contract.service';
import {
  CONTRACT_STATUS_OPTIONS,
  DEFAULT_PROBATION_DAYS,
  defaultContractStatus,
  type ContractFieldsModel,
} from './contract-fields.model';

@Component({
  selector: 'app-contract-fields',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './contract-fields.component.html',
  styleUrls: ['./contract-fields.component.css'],
})
export class ContractFieldsComponent {
  @Input({ required: true }) model!: ContractFieldsModel;
  @Output() modelChange = new EventEmitter<ContractFieldsModel>();

  /** create: startDate editable ; edit: startDate read-only */
  @Input() mode: 'create' | 'edit' = 'create';

  /** Show status select (create + edit) */
  @Input() showStatus = true;

  readonly contractStatuses = CONTRACT_STATUS_OPTIONS;
  readonly icons = { notes: FileText };

  readonly contractTypes: {
    value: ContractType;
    label: string;
    icon: IconNode;
    desc: string;
    cssClass: string;
  }[] = [
    { value: 'CDI', label: 'CDI', icon: ClipboardList, desc: 'Durée indéterminée', cssClass: 'type-card--cdi' },
    { value: 'CDD', label: 'CDD', icon: Calendar, desc: 'Durée déterminée', cssClass: 'type-card--cdd' },
    { value: 'Stage', label: 'Stage', icon: GraduationCap, desc: 'Stage de formation', cssClass: 'type-card--stage' },
    { value: 'ANAPEC', label: 'ANAPEC', icon: RefreshCw, desc: 'Mission temporaire', cssClass: 'type-card--anapec' },
  ];

  selectType(value: ContractType): void {
    const next = { ...this.model, type: value };
    if (value === 'CDI') {
      next.endDate = '';
    }
    if (value === 'ANAPEC') {
      next.probationDays = 0;
    }
    next.status = defaultContractStatus(next.probationDays, value);
    this.emit(next);
  }

  patch(partial: Partial<ContractFieldsModel>): void {
    const next = { ...this.model, ...partial };
    if (partial.probationDays !== undefined || partial.type !== undefined) {
      next.status = defaultContractStatus(next.probationDays, next.type);
    }
    this.emit(next);
  }

  onProbationChange(value: string): void {
    const parsed = value === '' ? null : Number(value);
    this.patch({ probationDays: Number.isFinite(parsed) ? parsed : null });
  }

  getDefaultProbationValue(): number {
    return DEFAULT_PROBATION_DAYS[this.model.type] ?? 0;
  }

  getDefaultProbationLabel(): string {
    return `Par défaut : ${this.getDefaultProbationValue()} jours`;
  }

  private emit(next: ContractFieldsModel): void {
    this.model = next;
    this.modelChange.emit(next);
  }
}
