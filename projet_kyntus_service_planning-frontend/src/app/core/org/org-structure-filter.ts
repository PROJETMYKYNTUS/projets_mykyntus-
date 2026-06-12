import type { Department, LegacyCellule, LegacyPole } from '../../features/prime/models';
import type { OrgFlatServiceOption } from './planning-org-picker';
import { poleCells } from './planning-org-picker';

export function normalizeOrgSearch(q: string): string {
  return q.trim().toLowerCase();
}

export function filterDepartmentsBySearch(departments: readonly Department[], search: string): Department[] {
  const q = normalizeOrgSearch(search);
  if (!q) return [...departments];
  return departments.filter((d) => d.name.toLowerCase().includes(q));
}

export function filterPolesBySearch(poles: readonly LegacyPole[], search: string): LegacyPole[] {
  const q = normalizeOrgSearch(search);
  if (!q) return [...poles];
  return poles.filter((p) => p.name.toLowerCase().includes(q));
}

export function filterCellulesBySearch(cells: readonly LegacyCellule[], search: string): LegacyCellule[] {
  const q = normalizeOrgSearch(search);
  if (!q) return [...cells];
  return cells.filter((c) => c.name.toLowerCase().includes(q));
}

export function filterFlatServiceOptions(
  options: readonly OrgFlatServiceOption[],
  opts: { search?: string; poleId?: string; celluleId?: string },
  limit = 40,
): { visible: OrgFlatServiceOption[]; totalMatches: number } {
  let matched = [...options];
  const q = normalizeOrgSearch(opts.search ?? '');
  if (q) matched = matched.filter((o) => o.label.toLowerCase().includes(q));
  if (opts.poleId) matched = matched.filter((o) => o.poleId === opts.poleId);
  if (opts.celluleId) matched = matched.filter((o) => o.celluleId === opts.celluleId);
  return { visible: matched.slice(0, limit), totalMatches: matched.length };
}

export function poleFilterOptions(departments: readonly Department[]): { id: string; name: string }[] {
  return departments.map((d) => ({ id: d.id, name: d.name }));
}

export function celluleFilterOptions(
  departments: readonly Department[],
  poleId: string,
): { id: string; name: string }[] {
  if (!poleId) return [];
  const dept = departments.find((d) => d.id === poleId);
  return (dept?.poles ?? []).map((p) => ({ id: p.id, name: p.name }));
}

export function orgSelectionSummary(
  departments: readonly Department[],
  poleId: string,
  celluleId: string,
  serviceId: string,
): string {
  const dept = departments.find((d) => d.id === poleId);
  const pole = dept?.poles?.find((p) => p.id === celluleId);
  const service = pole ? poleCells(pole).find((c) => c.id === serviceId) : undefined;
  return [dept?.name, pole?.name, service?.name].filter(Boolean).join(' / ') || '—';
}

export type OrgRhFilterSelection = {
  pole?: string;
  cellule?: string;
};

export type OrgRhFilterOptions = {
  poles: string[];
  cellules: string[];
  services: string[];
};

function sortOrgNames(names: Iterable<string>): string[] {
  return [...names].sort((a, b) => a.localeCompare(b, 'fr'));
}

/** Options de filtres pôle / cellule / service tirées de la structure Organisation RH (Prime). */
export function buildOrgRhFilterOptions(
  departments: readonly Department[],
  selection: OrgRhFilterSelection = {},
): OrgRhFilterOptions {
  const poles = sortOrgNames(
    departments.map((d) => d.name?.trim()).filter((n): n is string => !!n),
  );

  const scopedDepartments = selection.pole
    ? departments.filter((d) => d.name === selection.pole)
    : departments;

  const celluleSet = new Set<string>();
  for (const dept of scopedDepartments) {
    for (const cellule of dept.poles ?? []) {
      const name = cellule.name?.trim();
      if (name) celluleSet.add(name);
    }
  }
  const cellules = sortOrgNames(celluleSet);

  const serviceSet = new Set<string>();
  const scopedForServices = selection.cellule
    ? scopedDepartments.flatMap((dept) =>
        (dept.poles ?? [])
          .filter((cellule) => cellule.name === selection.cellule)
          .map((cellule) => ({ dept, cellule })),
      )
    : scopedDepartments.flatMap((dept) =>
        (dept.poles ?? []).map((cellule) => ({ dept, cellule })),
      );

  for (const { cellule } of scopedForServices) {
    for (const service of poleCells(cellule)) {
      const name = service.name?.trim();
      if (name) serviceSet.add(name);
    }
  }
  const services = sortOrgNames(serviceSet);

  return { poles, cellules, services };
}
