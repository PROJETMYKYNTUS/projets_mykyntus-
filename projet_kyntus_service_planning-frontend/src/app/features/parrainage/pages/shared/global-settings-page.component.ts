import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-global-settings-page',
  template: '',
})
export class GlobalSettingsPageComponent implements OnInit {
  private readonly router = inject(Router);
  ngOnInit(): void {
    void this.router.navigateByUrl('/settings');
  }
}
