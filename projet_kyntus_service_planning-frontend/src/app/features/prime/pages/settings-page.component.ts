import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';

/** Redirige vers les paramètres globaux du shell. */
@Component({
  standalone: true,
  selector: 'app-settings-page',
  template: '',
})
export class SettingsPageComponent implements OnInit {
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.router.navigateByUrl('/settings');
  }
}
