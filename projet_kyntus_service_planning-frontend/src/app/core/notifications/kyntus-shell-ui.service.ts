import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class KyntusShellUiService {
  readonly dropdownOpen = signal(false);
  readonly settingsOpen = signal(false);

  toggleDropdown(): void {
    this.dropdownOpen.update((o) => !o);
  }

  closeDropdown(): void {
    this.dropdownOpen.set(false);
  }

  openSettings(): void {
    this.settingsOpen.set(true);
    this.dropdownOpen.set(false);
  }

  closeSettings(): void {
    this.settingsOpen.set(false);
  }
}
