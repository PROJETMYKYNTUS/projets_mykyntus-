import { Injectable, signal } from '@angular/core';
import {
  DEFAULT_USER_PREFERENCES,
  KYNTHUS_NOTIFICATION_PREFS_KEY,
  type KyntusNotificationPreferences,
  type KyntusUserPreferences,
} from './kyntus-user-preferences.model';

@Injectable({ providedIn: 'root' })
export class KyntusUserPreferencesService {
  private readonly prefs = signal<KyntusUserPreferences>(this.load());

  readonly preferences = this.prefs.asReadonly();

  private load(): KyntusUserPreferences {
    if (typeof localStorage === 'undefined') return { ...DEFAULT_USER_PREFERENCES };
    try {
      const raw = localStorage.getItem(KYNTHUS_NOTIFICATION_PREFS_KEY);
      if (!raw) return { ...DEFAULT_USER_PREFERENCES };
      const parsed = JSON.parse(raw) as Partial<KyntusUserPreferences>;
      return {
        compactMode: parsed.compactMode ?? false,
        notifications: { ...DEFAULT_USER_PREFERENCES.notifications, ...parsed.notifications },
      };
    } catch {
      return { ...DEFAULT_USER_PREFERENCES };
    }
  }

  private persist(): void {
    localStorage.setItem(KYNTHUS_NOTIFICATION_PREFS_KEY, JSON.stringify(this.prefs()));
  }

  setCompactMode(compact: boolean): void {
    this.prefs.update((p) => ({ ...p, compactMode: compact }));
    this.persist();
  }

  setNotificationPref(key: keyof KyntusNotificationPreferences, enabled: boolean): void {
    this.prefs.update((p) => ({
      ...p,
      notifications: { ...p.notifications, [key]: enabled },
    }));
    this.persist();
  }

  isSourceEnabled(source: keyof KyntusNotificationPreferences): boolean {
    return this.prefs().notifications[source] ?? true;
  }
}
