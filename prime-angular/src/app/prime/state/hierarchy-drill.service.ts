import { Injectable, signal, effect, inject } from '@angular/core';
import type { HierarchyDrillSelection } from '../lib/hierarchyDrillDown';
import { RoleService } from './role.service';

export type { HierarchyDrillSelection } from '../lib/hierarchyDrillDown';

@Injectable({ providedIn: 'root' })
export class HierarchyDrillService {
  private readonly roleService = inject(RoleService);
  readonly drill = signal<HierarchyDrillSelection>({});

  constructor() {
    effect(() => {
      this.roleService.currentRole();
      this.resetDrill();
    });
  }

  setManagerId(id: string | undefined): void {
    this.drill.update((d) => ({ ...d, managerId: id, coachId: undefined }));
  }

  setCoachId(id: string | undefined): void {
    this.drill.update((d) => ({ ...d, coachId: id }));
  }

  resetDrill(): void {
    this.drill.set({});
  }
}
