import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, throwError, timer } from 'rxjs';
import { catchError, filter, map, switchMap, take, timeout } from 'rxjs/operators';
import type { Cellule, Department, Employee, Pole } from '../models';

const orgBase = '/api/directory/org';
const directoryBase = '/api/directory';
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
  celluleId?: string;
  serviceId: string;
}

export interface CoachSousServiceAssignmentDto {
  id: string;
  userId: string;
  serviceId?: string;
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

export interface EnsureEmployeeFromPlanningDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  primeServiceId?: string | null;
}

interface DirectoryOverviewResponse {
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
    return this.http.get<SupervisorOrgScopePole[]>(`${primeBase}/org/supervisor-scope`, { params });
  }

  loadOverview(): Observable<OrgAssignmentsOverview> {
    return this.http.get<DirectoryOverviewResponse>(`${orgBase}/overview`).pipe(
      map((d) => ({
        etages: d.etages ?? [],
        services: (d.services ?? []).map((s) => ({
          id: s.id,
          name: s.name,
          etageId: (s as { etageId?: string }).etageId ?? '',
        })),
        sousServices: (d.sousServices ?? []).map((s) => ({
          id: s.id,
          name: s.name,
          serviceId: (s as { serviceId?: string }).serviceId ?? '',
        })),
        employees: (d.employees ?? []).map((e) => ({
          ...e,
          id: (e as { id?: string }).id ?? '',
          poleId: (e as { poleId?: string }).poleId ?? '',
          celluleId: (e as { celluleId?: string }).celluleId ?? '',
          serviceId: (e as { serviceId?: string }).serviceId ?? '',
        })),
        departments: d.departments ?? [],
        managerEtage: d.managerEtage ?? [],
        supervisorService: (d.supervisorService ?? []).map((a) => ({
          id: a.id,
          userId: a.userId,
          celluleId: a.celluleId ?? (a as { serviceId?: string }).serviceId,
          serviceId: (a.celluleId ?? (a as { serviceId?: string }).serviceId ?? '').trim(),
        })),
        coachSousService: (d.coachSousService ?? []).map((a) => {
          const sid = (
            (a as { serviceId?: string }).serviceId ??
            (a as { sousServiceId?: string }).sousServiceId ??
            ''
          ).trim();
          return { id: a.id, userId: a.userId, serviceId: sid, sousServiceId: sid };
        }),
        coachPilot: d.coachPilot ?? [],
      })),
    );
  }

  ensureEmployeeFromPlanning(dto: EnsureEmployeeFromPlanningDto): Observable<{ employeeId: string }> {
    return this.http
      .post<{ id: string }>(`${directoryBase}/employees`, {
        employeeId: dto.employeeId || null,
        firstName: dto.firstName,
        lastName: dto.lastName,
        email: dto.email,
        role: dto.role,
        serviceId: dto.primeServiceId ?? null,
      })
      .pipe(map((r) => ({ employeeId: r.id })));
  }

  waitForEmployee(employeeId: string, maxWaitMs = 15000): Observable<void> {
    const id = employeeId.trim();
    if (!id) {
      return throwError(() => new Error('Identifiant employé manquant.'));
    }
    return timer(0, 500).pipe(
      switchMap(() => this.http.get<Employee[]>(`${directoryBase}/employees`)),
      map((employees) => employees.some((employee) => (employee as { id?: string }).id === id)),
      filter(Boolean),
      take(1),
      timeout({ first: maxWaitMs }),
      map(() => undefined),
      catchError(() =>
        throwError(
          () =>
            new Error(
              'Employé non encore synchronisé dans l\'annuaire. Réessayez dans quelques secondes.',
            ),
        ),
      ),
    );
  }

  assignManagerEtage(userId: string, etageId: string): Observable<ManagerEtageAssignmentDto> {
    return this.http
      .post<void>(`${orgBase}/assignments/ChefDeProjet/${encodeURIComponent(etageId)}`, {
        employeeId: userId,
      })
      .pipe(
        map(() => ({
          id: `m|${userId}|${etageId}`,
          userId,
          etageId,
        })),
      );
  }

  assignSupervisorService(userId: string, serviceId: string): Observable<SupervisorServiceAssignmentDto> {
    return this.http
      .post<void>(`${orgBase}/assignments/Superviseur/${encodeURIComponent(serviceId)}`, {
        employeeId: userId,
      })
      .pipe(
        map(() => ({
          id: `s|${userId}|${serviceId}`,
          userId,
          celluleId: serviceId,
          serviceId,
        })),
      );
  }

  assignCoachSousService(userId: string, sousServiceId: string): Observable<CoachSousServiceAssignmentDto> {
    return this.http
      .post<void>(`${orgBase}/assignments/ReferentTechnique/${encodeURIComponent(sousServiceId)}`, {
        employeeId: userId,
      })
      .pipe(
        map(() => ({
          id: `c|${userId}|${sousServiceId}`,
          userId,
          serviceId: sousServiceId,
          sousServiceId,
        })),
      );
  }

  assignCoachPilot(coachUserId: string, pilotUserId: string): Observable<CoachPilotLinkDto> {
    return this.http.post<CoachPilotLinkDto>(`${primeBase}/org/assignments/coach-pilot`, {
      coachUserId,
      pilotUserId,
    });
  }

  removeManagerEtage(id: string): Observable<unknown> {
    const etageId = id.split('|')[2] ?? id;
    return this.http.delete(`${orgBase}/assignments/ChefDeProjet/${encodeURIComponent(etageId)}`);
  }

  removeSupervisorService(id: string): Observable<unknown> {
    const celluleId = id.split('|')[2] ?? id;
    return this.http.delete(`${orgBase}/assignments/Superviseur/${encodeURIComponent(celluleId)}`);
  }

  removeCoachSousService(id: string): Observable<unknown> {
    const serviceId = id.split('|')[2] ?? id;
    return this.http.delete(`${orgBase}/assignments/ReferentTechnique/${encodeURIComponent(serviceId)}`);
  }

  removeCoachPilot(id: string): Observable<unknown> {
    return this.http.delete(`${primeBase}/org/assignments/coach-pilot/${encodeURIComponent(id)}`);
  }

  setStructureManager(departmentId: string, employeeId: string): Observable<unknown> {
    return this.assignManagerEtage(employeeId, departmentId);
  }

  clearStructureManager(departmentId: string): Observable<unknown> {
    return this.removeManagerEtage(`m|*|${departmentId}`);
  }

  setStructureSupervisor(poleId: string, employeeId: string): Observable<unknown> {
    return this.assignSupervisorService(employeeId, poleId);
  }

  clearStructureSupervisor(poleId: string): Observable<unknown> {
    return this.removeSupervisorService(`s|*|${poleId}`);
  }

  setStructureCoach(celluleId: string, employeeId: string): Observable<unknown> {
    return this.assignCoachSousService(employeeId, celluleId);
  }

  clearStructureCoach(celluleId: string): Observable<unknown> {
    return this.removeCoachSousService(`c|*|${celluleId}`);
  }

  addStructurePilot(celluleId: string, employeeId: string, teamId?: string | null): Observable<unknown> {
    return this.http.post(`${primeBase}/org/structure/cellules/${encodeURIComponent(celluleId)}/pilots`, {
      employeeId,
      teamId: teamId || null,
    });
  }

  removeStructurePilot(celluleId: string, employeeId: string): Observable<unknown> {
    return this.http.delete(
      `${primeBase}/org/structure/cellules/${encodeURIComponent(celluleId)}/pilots/${encodeURIComponent(employeeId)}`,
    );
  }

  createStructureDepartment(name: string): Observable<Department> {
    return this.http.post<{ id: string }>(`${orgBase}/structure/poles`, { name: name.trim() }).pipe(
      map((r) => ({ id: r.id, name: name.trim(), poles: [] } as Department)),
    );
  }

  createStructurePole(departmentId: string, name: string): Observable<Pole> {
    return this.http
      .post<{ id: string }>(`${orgBase}/structure/poles/${encodeURIComponent(departmentId)}/cellules`, {
        name: name.trim(),
      })
      .pipe(map((r) => ({ id: r.id, name: name.trim(), cellules: [] } as Pole)));
  }

  createStructureCellule(poleId: string, name: string): Observable<Cellule> {
    return this.http
      .post<{ id: string }>(`${orgBase}/structure/cellules/${encodeURIComponent(poleId)}/services`, {
        name: name.trim(),
      })
      .pipe(map((r) => ({ id: r.id, name: name.trim(), poleId, services: [] } as Cellule)));
  }
}
