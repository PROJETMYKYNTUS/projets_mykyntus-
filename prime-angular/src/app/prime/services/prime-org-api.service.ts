import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import type { Cellule, Department, Employee, Pole } from '../models';

const orgBase = '/api/prime/org';
const primeBase = '/api/prime';

export interface EtageNodeDto {
  id: string;
  name: string;
}

export interface ServiceNodeDto {
  id: string;
  name: string;
  etageId: string;
}

export interface SousServiceNodeDto {
  id: string;
  name: string;
  serviceId: string;
}

export interface ManagerEtageAssignmentDto {
  id: string;
  userId: string;
  etageId: string;
}

export interface SupervisorServiceAssignmentDto {
  id: string;
  userId: string;
  /** Identifiant cellule (réponse API .NET : <c>celluleId</c>). */
  celluleId?: string;
  /** Alias / champ normalisé côté client (identifiant cellule). */
  serviceId: string;
}

export interface CoachSousServiceAssignmentDto {
  id: string;
  userId: string;
  /** Réponse API .NET : <c>serviceId</c> (service feuille). */
  serviceId?: string;
  /** Champ normalisé côté client (= service feuille). */
  sousServiceId: string;
}

export interface CoachPilotLinkDto {
  id: string;
  coachUserId: string;
  pilotUserId: string;
}

export interface SupervisorOrgScopeService {
  id: string;
  name: string;
}

export interface SupervisorOrgScopeCellule {
  id: string;
  name: string;
  rootPoleId: string;
  services: SupervisorOrgScopeService[];
}

export interface SupervisorOrgScopePole {
  id: string;
  name: string;
  cellules: SupervisorOrgScopeCellule[];
}

export interface OrgAssignmentsOverview {
  etages: EtageNodeDto[];
  services: ServiceNodeDto[];
  sousServices: SousServiceNodeDto[];
  employees: Employee[];
  departments: Department[];
  managerEtage: ManagerEtageAssignmentDto[];
  supervisorService: SupervisorServiceAssignmentDto[];
  coachSousService: CoachSousServiceAssignmentDto[];
  coachPilot: CoachPilotLinkDto[];
}

@Injectable({ providedIn: 'root' })
export class PrimeOrgApiService {
  private readonly http = inject(HttpClient);

  getSupervisorScope(supervisorUserId: string): Observable<SupervisorOrgScopePole[]> {
    const params = { supervisorUserId };
    return this.http.get<SupervisorOrgScopePole[]>(`${orgBase}/supervisor-scope`, { params });
  }

  loadOverview(): Observable<OrgAssignmentsOverview> {
    return forkJoin({
      etages: this.http.get<EtageNodeDto[]>(`${orgBase}/etages`),
      services: this.http.get<ServiceNodeDto[]>(`${orgBase}/services`),
      sousServices: this.http.get<SousServiceNodeDto[]>(`${orgBase}/sous-services`),
      employees: this.http.get<Employee[]>(`${primeBase}/employees`),
      departments: this.http.get<Department[]>(`${primeBase}/departments`),
      managerEtage: this.http.get<ManagerEtageAssignmentDto[]>(`${orgBase}/assignments/manager-etage`),
      supervisorService: this.http.get<SupervisorServiceAssignmentDto[]>(
        `${orgBase}/assignments/supervisor-service`,
      ),
      coachSousService: this.http.get<CoachSousServiceAssignmentDto[]>(
        `${orgBase}/assignments/coach-sous-service`,
      ),
      coachPilot: this.http.get<CoachPilotLinkDto[]>(`${orgBase}/assignments/coach-pilot`),
    }).pipe(
      map((d) => ({
        ...d,
        supervisorService: d.supervisorService.map((a) => ({
          id: a.id,
          userId: a.userId,
          celluleId: a.celluleId ?? (a as { serviceId?: string }).serviceId,
          serviceId: (a.celluleId ?? (a as { serviceId?: string }).serviceId ?? '').trim(),
        })),
        coachSousService: d.coachSousService.map((a) => {
          const sid = (
            (a as { serviceId?: string }).serviceId ??
            (a as { sousServiceId?: string }).sousServiceId ??
            ''
          ).trim();
          return { id: a.id, userId: a.userId, serviceId: sid, sousServiceId: sid };
        }),
      })),
    );
  }

  assignManagerEtage(userId: string, etageId: string): Observable<ManagerEtageAssignmentDto> {
    return this.http.post<ManagerEtageAssignmentDto>(`${orgBase}/assignments/manager-etage`, {
      userId,
      etageId,
    });
  }

  assignSupervisorService(userId: string, serviceId: string): Observable<SupervisorServiceAssignmentDto> {
    return this.http.post<SupervisorServiceAssignmentDto>(`${orgBase}/assignments/supervisor-service`, {
      userId,
      serviceId,
    });
  }

  assignCoachSousService(userId: string, sousServiceId: string): Observable<CoachSousServiceAssignmentDto> {
    return this.http.post<CoachSousServiceAssignmentDto>(`${orgBase}/assignments/coach-sous-service`, {
      userId,
      sousServiceId,
    });
  }

  assignCoachPilot(coachUserId: string, pilotUserId: string): Observable<CoachPilotLinkDto> {
    return this.http.post<CoachPilotLinkDto>(`${orgBase}/assignments/coach-pilot`, {
      coachUserId,
      pilotUserId,
    });
  }

  removeManagerEtage(id: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/assignments/manager-etage/${encodeURIComponent(id)}`);
  }

  removeSupervisorService(id: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/assignments/supervisor-service/${encodeURIComponent(id)}`);
  }

  removeCoachSousService(id: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/assignments/coach-sous-service/${encodeURIComponent(id)}`);
  }

  removeCoachPilot(id: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/assignments/coach-pilot/${encodeURIComponent(id)}`);
  }

  /** Remplace le manager du département (rôle + affectation, transaction serveur). */
  setStructureManager(departmentId: string, employeeId: string): Observable<unknown> {
    return this.http.post(`${orgBase}/structure/departments/${encodeURIComponent(departmentId)}/manager`, {
      employeeId,
    });
  }

  clearStructureManager(departmentId: string): Observable<unknown> {
    return this.http.delete(
      `${orgBase}/structure/departments/${encodeURIComponent(departmentId)}/manager`,
    );
  }

  setStructureSupervisor(poleId: string, employeeId: string): Observable<unknown> {
    return this.http.post(`${orgBase}/structure/poles/${encodeURIComponent(poleId)}/supervisor`, {
      employeeId,
    });
  }

  clearStructureSupervisor(poleId: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/structure/poles/${encodeURIComponent(poleId)}/supervisor`);
  }

  setStructureCoach(celluleId: string, employeeId: string): Observable<unknown> {
    return this.http.post(`${orgBase}/structure/cellules/${encodeURIComponent(celluleId)}/coach`, {
      employeeId,
    });
  }

  clearStructureCoach(celluleId: string): Observable<unknown> {
    return this.http.delete(`${orgBase}/structure/cellules/${encodeURIComponent(celluleId)}/coach`);
  }

  addStructurePilot(celluleId: string, employeeId: string, teamId?: string | null): Observable<unknown> {
    return this.http.post(`${orgBase}/structure/cellules/${encodeURIComponent(celluleId)}/pilots`, {
      employeeId,
      teamId: teamId || null,
    });
  }

  removeStructurePilot(celluleId: string, employeeId: string): Observable<unknown> {
    return this.http.delete(
      `${orgBase}/structure/cellules/${encodeURIComponent(celluleId)}/pilots/${encodeURIComponent(employeeId)}`,
    );
  }

  createStructureDepartment(name: string): Observable<Department> {
    return this.http.post<Department>(`${orgBase}/structure/departments`, { name: name.trim() });
  }

  createStructurePole(departmentId: string, name: string): Observable<Pole> {
    return this.http.post<Pole>(`${orgBase}/structure/departments/${encodeURIComponent(departmentId)}/poles`, {
      name: name.trim(),
    });
  }

  createStructureCellule(poleId: string, name: string): Observable<Cellule> {
    return this.http.post<Cellule>(`${orgBase}/structure/poles/${encodeURIComponent(poleId)}/cellules`, {
      name: name.trim(),
    });
  }
}
