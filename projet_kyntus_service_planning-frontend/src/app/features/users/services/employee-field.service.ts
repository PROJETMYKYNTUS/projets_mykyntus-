import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { EmployeeImportFieldConfig } from './employee-import.service';

export const EMPLOYEE_FIELDS_API_BASE = '/api/users/fields';

export interface CreateEmployeeFieldRequest {
  label: string;
  fieldKey?: string;
  dataType: string;
  isRequiredOnCreate: boolean;
  isEnabled: boolean;
  aliases: string[];
  sortOrder?: number;
}

export interface UpdateEmployeeFieldRequest {
  label: string;
  dataType: string;
  isRequiredOnCreate: boolean;
  isEnabled: boolean;
  aliases: string[];
  sortOrder: number;
}

@Injectable({ providedIn: 'root' })
export class EmployeeFieldService {
  private readonly http = inject(HttpClient);
  private readonly base = EMPLOYEE_FIELDS_API_BASE;

  getFields(enabledOnly = false): Observable<EmployeeImportFieldConfig[]> {
    return this.http.get<EmployeeImportFieldConfig[]>(this.base, {
      params: enabledOnly ? { enabledOnly: 'true' } : {},
    });
  }

  createField(request: CreateEmployeeFieldRequest): Observable<EmployeeImportFieldConfig> {
    return this.http.post<EmployeeImportFieldConfig>(this.base, request);
  }

  updateField(fieldKey: string, request: UpdateEmployeeFieldRequest): Observable<EmployeeImportFieldConfig> {
    return this.http.put<EmployeeImportFieldConfig>(`${this.base}/${encodeURIComponent(fieldKey)}`, request);
  }

  deleteField(fieldKey: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(fieldKey)}`);
  }
}
