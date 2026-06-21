import type { Department, Employee } from '../models';

export interface EmployeeOrgDisplayLabels {
  operationalDepartment: string;
  pole: string;
  cellule: string;
  service: string;
}

const MISSING = '—';

/**
 * Résout les libellés affichables pour l’arbre legacy API :
 * Department = pôle DB, poles[] = cellules DB, cells[] = services DB.
 */
export function resolveEmployeeOrgLabels(
  employee: Employee,
  departments: readonly Department[],
): EmployeeOrgDisplayLabels {
  const poleKey = (employee.departementId ?? employee.poleId ?? '').trim();
  const celluleKey = (employee.celluleId ?? '').trim();
  const serviceKey = (employee.serviceId ?? employee.teamId ?? '').trim();

  let pole = MISSING;
  let cellule = MISSING;
  let service = MISSING;

  const department =
    departments.find((d) => d.id === poleKey) ??
    departments.find((d) => d.poles.some((p) => p.id === celluleKey || p.id === poleKey));

  if (department) {
    pole = department.name?.trim() || MISSING;

    const celluleNode =
      department.poles.find((p) => p.id === celluleKey) ??
      department.poles.find((p) => p.id === poleKey);
    if (celluleNode) {
      cellule = celluleNode.name?.trim() || MISSING;

      const serviceNode = celluleNode.cells.find(
        (c) => c.id === serviceKey || c.teams?.some((t) => t.id === serviceKey || t.id === `${serviceKey}-team`),
      );
      if (serviceNode) {
        service = serviceNode.name?.trim() || MISSING;
      } else {
        const team = celluleNode.cells
          .flatMap((c) => c.teams ?? [])
          .find((t) => t.id === serviceKey || t.id === `${serviceKey}-team` || (t as { serviceId?: string }).serviceId === serviceKey);
        if (team) service = team.name?.trim() || MISSING;
      }
    }
  }

  if (service === MISSING && serviceKey) {
    for (const d of departments) {
      for (const p of d.poles) {
        for (const c of p.cells) {
          if (c.id === serviceKey) {
            return {
              operationalDepartment: MISSING,
              pole: d.name?.trim() || pole,
              cellule: p.name?.trim() || cellule,
              service: c.name?.trim() || MISSING,
            };
          }
          for (const t of c.teams ?? []) {
            const sid = (t as { serviceId?: string }).serviceId;
            if (t.id === serviceKey || t.id === `${serviceKey}-team` || sid === serviceKey) {
              return {
                operationalDepartment: MISSING,
                pole: d.name?.trim() || pole,
                cellule: p.name?.trim() || cellule,
                service: t.name?.trim() || c.name?.trim() || MISSING,
              };
            }
          }
        }
      }
    }
  }

  return { operationalDepartment: MISSING, pole, cellule, service };
}
