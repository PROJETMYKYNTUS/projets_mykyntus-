import type { Department, LegacyCellule, LegacyPole } from '../../features/prime/models';

export type OrgPickerSelection = {
  poleId: string;
  celluleId: string;
  serviceId: string;
};

export type OrgFlatServiceOption = {
  serviceId: string;
  poleId: string;
  celluleId: string;
  label: string;
};

/** Cellules d’un pôle (alias API `cellules` / `cells`). */
export function poleCells(pole: LegacyPole): LegacyCellule[] {
  return pole.cells ?? (pole as LegacyPole & { cellules?: LegacyCellule[] }).cellules ?? [];
}

/** Tous les services feuilles (Organisation RH) avec fil d’Ariane pôle / cellule / service. */
export function flattenOrgServiceOptions(departments: readonly Department[]): OrgFlatServiceOption[] {
  const out: OrgFlatServiceOption[] = [];
  for (const dept of departments) {
    for (const pole of dept.poles ?? []) {
      for (const cell of poleCells(pole)) {
        out.push({
          serviceId: cell.id,
          poleId: dept.id,
          celluleId: pole.id,
          label: `${dept.name} / ${pole.name} / ${cell.name}`,
        });
      }
    }
  }
  return out.sort((a, b) => a.label.localeCompare(b.label, 'fr'));
}

export function findOrgSelectionByPrimeServiceId(
  departments: readonly Department[],
  primeServiceId: string,
): OrgPickerSelection | null {
  const sid = primeServiceId.trim();
  if (!sid) return null;
  for (const dept of departments) {
    for (const pole of dept.poles ?? []) {
      for (const cell of poleCells(pole)) {
        if (cell.id === sid) {
          return { poleId: dept.id, celluleId: pole.id, serviceId: cell.id };
        }
        for (const team of cell.teams ?? []) {
          if (team.id === sid) {
            return { poleId: dept.id, celluleId: pole.id, serviceId: cell.id };
          }
        }
      }
    }
  }
  return null;
}

export function resolveSubServiceIdByPrimeServiceId(
  subServices: readonly { id: number; primeServiceId?: string | null }[],
  primeServiceId: string,
): number | null {
  const sid = primeServiceId.trim();
  if (!sid) return null;
  const hit = subServices.find((s) => (s.primeServiceId ?? '').trim() === sid);
  return hit?.id ?? null;
}

export function resolvePlanningServiceIdByPrimeCelluleId(
  services: readonly { id: number; primeCelluleId?: string | null }[],
  primeCelluleId: string,
): number | null {
  const cid = primeCelluleId.trim();
  if (!cid) return null;
  const hit = services.find((s) => (s.primeCelluleId ?? '').trim() === cid);
  return hit?.id ?? null;
}
