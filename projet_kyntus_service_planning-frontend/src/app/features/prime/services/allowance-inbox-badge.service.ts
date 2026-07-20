import { Injectable, inject, signal } from '@angular/core';
import { AllowanceApiService } from './allowance-api.service';

@Injectable({ providedIn: 'root' })
export class AllowanceInboxBadgeService {
  private readonly api = inject(AllowanceApiService);

  readonly count = signal(0);
  private refreshPromise: Promise<void> | null = null;

  refreshForRole(role: string): Promise<void> {
    if (this.refreshPromise) return this.refreshPromise;
    this.refreshPromise = this.doRefresh(role).finally(() => {
      this.refreshPromise = null;
    });
    return this.refreshPromise;
  }

  private async doRefresh(role: string): Promise<void> {
    // Inbox RH uniquement — sans fiche Prime (JWT non projeté) le backend répondait 403.
    if (role !== 'RH') {
      this.count.set(0);
      return;
    }
    try {
      const rows = await this.api.inbox();
      this.count.set(Array.isArray(rows) ? rows.length : 0);
    } catch {
      this.count.set(0);
    }
  }
}
