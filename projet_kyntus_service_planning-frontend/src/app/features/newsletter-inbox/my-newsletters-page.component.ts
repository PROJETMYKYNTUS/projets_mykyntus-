import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Inbox, Loader2, Mail, MailOpen, Search } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { NewsletterService, EmployeeNewsletter } from '../../core/services/newsletter.service';

@Component({
  selector: 'app-my-newsletters-page',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './my-newsletters-page.component.html',
  styleUrls: ['./my-newsletters-page.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class MyNewslettersPageComponent implements OnInit {
  readonly icons = { mail: Mail, mailOpen: MailOpen, search: Search, inbox: Inbox, loader: Loader2 };

  myNewsletters: EmployeeNewsletter[] = [];
  filteredNewsletters: EmployeeNewsletter[] = [];
  selectedNewsletter: EmployeeNewsletter | null = null;
  searchTerm = '';
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
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.myNewsletters = [];
        this.filteredNewsletters = [];
        this.loading = false;
        this.error = 'Impossible de charger vos newsletters.';
      },
    });
  }

  applyFilters(): void {
    const q = this.searchTerm.trim().toLowerCase();
    this.filteredNewsletters = q
      ? this.myNewsletters.filter(
          (nl) =>
            nl.newsletterTitle.toLowerCase().includes(q) ||
            nl.newsletterSubject.toLowerCase().includes(q) ||
            nl.campaignName.toLowerCase().includes(q),
        )
      : [...this.myNewsletters];
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.applyFilters();
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
    return this.myNewsletters.filter((n) => n.isRead).length;
  }

  getUnreadCount(): number {
    return this.myNewsletters.filter((n) => !n.isRead).length;
  }

  constructor(private readonly newsletterSvc: NewsletterService) {}
}
