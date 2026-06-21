import type { DocumentationRole } from '../interfaces/documentation-role';
import {
  visibleEmployeeIdsForRole,
  type HierarchyDrillSelection,
} from './documentation-org-hierarchy';
import type { DirectoryUserDto } from '../../../core/models/documentation.models';

export function filterByEmployeeScope<T extends { employeeId?: string }>(
  items: T[],
  role: DocumentationRole,
  viewer: DirectoryUserDto | null,
  users: DirectoryUserDto[],
  drill?: HierarchyDrillSelection,
  supportDepartmentManager = false,
): T[] {
  const allowed = visibleEmployeeIdsForRole(role, viewer, users, drill ?? {}, supportDepartmentManager);
  if (allowed === null) return items;
  return items.filter((i) => i.employeeId && allowed.has(i.employeeId));
}

export type { HierarchyDrillSelection };
