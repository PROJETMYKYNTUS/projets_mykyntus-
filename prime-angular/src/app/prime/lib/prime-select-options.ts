import type { Employee } from '../models';

/** Options de liste incluant le titulaire courant même s'il a un rôle « protégé » (chef de projet, etc.). */
export function employeesForSelect(all: Employee[], selectedUserId?: string | null): Employee[] {
  const base = [...all];
  if (!selectedUserId) return base;
  const holder = all.find((e) => e.id === selectedUserId);
  if (holder && !base.some((e) => e.id === holder.id)) base.unshift(holder);
  return base;
}

/** Valeur sûre pour [value] d'un select : vide si l'id n'est pas dans les options. */
export function selectValueOrEmpty(selectedId: string | null | undefined, optionIds: readonly string[]): string {
  const id = (selectedId ?? '').trim();
  if (!id) return '';
  return optionIds.includes(id) ? id : '';
}
