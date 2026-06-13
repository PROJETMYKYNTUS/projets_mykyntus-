import type { Department } from '../../features/prime/models';
import type { OrgAssignmentsOverview } from '../../features/prime/services/prime-org-api.service';
import type { User } from '../../features/users/users-module';
import { findOrgSelectionByPrimeServiceId, poleCells } from './planning-org-picker';
import {
  isChefDeProjetRole,
  isReferentTechniqueRole,
  isSuperviseurRole,
} from './org-role-assignment';

export type UserOrgPerimeterView = {
  pole: string | null;
  cellule: string | null;
  service: string | null;
};

export function orgCellLabel(value: string | null | undefined): string {
  return value?.trim() || '—';
}

export function orgPerimeterSummary(view: UserOrgPerimeterView): string {
  const parts = [view.pole, view.cellule, view.service].filter((p) => !!p?.trim());
  return parts.length ? parts.join(' / ') : '—';
}

export function orgPerimeterFromUser(user: User): UserOrgPerimeterView {
  return {
    pole: user.orgPoleName?.trim() || null,
    cellule: user.orgCelluleName?.trim() || null,
    service: user.orgServiceName?.trim() || user.subServiceName?.trim() || null,
  };
}

function namesFromSelection(departments: readonly Department[], sel: { poleId: string; celluleId: string; serviceId: string }): UserOrgPerimeterView {
  const dept = departments.find((d) => d.id === sel.poleId);
  const pole = dept?.poles?.find((p) => p.id === sel.celluleId);
  const cell = pole ? poleCells(pole).find((c) => c.id === sel.serviceId) : undefined;
  return {
    pole: dept?.name ?? null,
    cellule: pole?.name ?? null,
    service: cell?.name ?? null,
  };
}

export function enrichUserOrgPerimeter(
  user: User,
  departments: readonly Department[],
  overview: OrgAssignmentsOverview | null,
  subServices: readonly { id: number; primeServiceId?: string | null }[],
): UserOrgPerimeterView {
  const base = orgPerimeterFromUser(user);
  if (base.pole?.trim()) return base;

  const guid = (user.guid ?? '').trim();
  if (!overview || !guid) return base;

  const primeEmployee = overview.employees?.find(
    (employee) => employee.id.trim().toLowerCase() === guid.toLowerCase(),
  );

  const mgr = overview.managerEtage?.find(
    (a) => a.userId.trim().toLowerCase() === guid.toLowerCase(),
  );
  if (mgr) {
    const dept = departments.find((d) => d.id === mgr.etageId);
    if (dept) return { pole: dept.name, cellule: null, service: null };
  }

  if (primeEmployee && isChefDeProjetRole(primeEmployee.role) && primeEmployee.poleId?.trim()) {
    const dept = departments.find((d) => d.id === primeEmployee.poleId);
    if (dept) return { pole: dept.name, cellule: null, service: null };
  }

  const sup = overview.supervisorService?.find(
    (a) => a.userId.trim().toLowerCase() === guid.toLowerCase(),
  );
  if (sup) {
    const celluleId = (sup.celluleId ?? sup.serviceId ?? '').trim();
    for (const dept of departments) {
      for (const pole of dept.poles ?? []) {
        if (pole.id === celluleId) {
          return { pole: dept.name, cellule: pole.name, service: null };
        }
      }
    }
  }

  if (primeEmployee && isSuperviseurRole(primeEmployee.role) && primeEmployee.celluleId?.trim()) {
    for (const dept of departments) {
      for (const pole of dept.poles ?? []) {
        if (pole.id === primeEmployee.celluleId) {
          return { pole: dept.name, cellule: pole.name, service: null };
        }
      }
    }
  }

  const coach = overview.coachSousService?.find(
    (a) => a.userId.trim().toLowerCase() === guid.toLowerCase(),
  );
  if (coach) {
    const svcId = (coach.serviceId ?? coach.sousServiceId ?? '').trim();
    if (svcId) {
      const sel = findOrgSelectionByPrimeServiceId(departments, svcId);
      if (sel) return namesFromSelection(departments, sel);
    }
  }

  if (primeEmployee && isReferentTechniqueRole(primeEmployee.role) && primeEmployee.serviceId?.trim()) {
    const sel = findOrgSelectionByPrimeServiceId(departments, primeEmployee.serviceId);
    if (sel) return namesFromSelection(departments, sel);
  }

  if (user.subServiceId) {
    const sub = subServices.find((s) => s.id === user.subServiceId);
    const primeId = sub?.primeServiceId?.trim();
    if (primeId) {
      const sel = findOrgSelectionByPrimeServiceId(departments, primeId);
      if (sel) return namesFromSelection(departments, sel);
    }
  }

  return base;
}
