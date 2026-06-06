import { Injectable, signal } from '@angular/core';

/** État local des panneaux cloche / paramètres dans l'en-tête Documentation. */
@Injectable({ providedIn: 'root' })
export class DocumentationHeaderUiService {
  readonly notifOpen = signal(false);
  readonly settingsOpen = signal(false);

  toggleNotif(): void {
    const next = !this.notifOpen();
    this.notifOpen.set(next);
    if (next) this.settingsOpen.set(false);
  }

  toggleSettings(): void {
    const next = !this.settingsOpen();
    this.settingsOpen.set(next);
    if (next) this.notifOpen.set(false);
  }

  closeNotif(): void {
    this.notifOpen.set(false);
  }

  closeSettings(): void {
    this.settingsOpen.set(false);
  }

  closeAll(): void {
    this.notifOpen.set(false);
    this.settingsOpen.set(false);
  }
}
