import { Injectable, signal } from '@angular/core';
import type { HierarchyDrillSelection } from '../lib/org-hierarchy';

@Injectable({ providedIn: 'root' })
export class HierarchyDrillService {
  readonly drill = signal<HierarchyDrillSelection>({});

  setDrill(partial: HierarchyDrillSelection): void {
    this.drill.update((d) => ({ ...d, ...partial }));
  }

  reset(): void {
    this.drill.set({});
  }
}
