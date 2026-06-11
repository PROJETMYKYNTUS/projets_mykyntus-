import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NewsletterService, EmployeeNewsletter } from '../../core/services/newsletter.service';

@Component({
  selector: 'app-my-newsletters-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './my-newsletters-page.component.html',
  styleUrls: ['./my-newsletters-page.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class MyNewslettersPageComponent implements OnInit {
  myNewsletters: EmployeeNewsletter[] = [];
  selectedNewsletter: EmployeeNewsletter | null = null;
  loading = false;
  error = '';

  ngOnInit(): void {
    this.loadMyNewsletters();
  }

  loadMyNewsletters(): void {
    this.loading = true;
    this.error = '';
    this.newsletterSvc.getMyNewsletters().subscribe({
      next: (data) => {
        this.myNewsletters = data ?? [];
        this.loading = false;
      },
      error: () => {
        this.myNewsletters = [];
        this.loading = false;
        this.error = 'Impossible de charger vos newsletters.';
      },
    });
  }

  openNewsletter(nl: EmployeeNewsletter): void {
    this.selectedNewsletter = nl;
    if (!nl.isRead) {
      this.newsletterSvc.markAsRead(nl.analyticsId).subscribe({
        next: () => {
          nl.isRead = true;
          nl.readAt = new Date().toISOString();
        },
      });
    }
  }

  closeNewsletter(): void {
    this.selectedNewsletter = null;
  }

  getReadCount(): number {
    return this.myNewsletters.filter(n => n.isRead).length;
  }

  getUnreadCount(): number {
    return this.myNewsletters.filter(n => !n.isRead).length;
  }

  constructor(private readonly newsletterSvc: NewsletterService) {}
}
