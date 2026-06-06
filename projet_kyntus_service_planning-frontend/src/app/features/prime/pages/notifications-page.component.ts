import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';

/** Redirige vers le centre de notifications unifié du shell (filtre PRIME). */
@Component({
  standalone: true,
  selector: 'app-notifications-page',
  template: '',
})
export class NotificationsPageComponent implements OnInit {
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.router.navigate(['/notifications'], { queryParams: { source: 'prime' } });
  }
}
