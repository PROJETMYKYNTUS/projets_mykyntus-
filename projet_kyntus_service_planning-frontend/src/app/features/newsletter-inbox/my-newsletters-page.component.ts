import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Inbox, Loader2, Mail, MailOpen, Search } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../shared/components/ui/kyntus-page-header.component';
import { EmployeeNewsletter, NewsletterService } from '../../core/services/newsletter.service';
import { NewsletterReaderComponent } from './newsletter-reader.component';
import { KyAuthMediaImgComponent } from '../../shared/components/ui/ky-auth-media-img.component';

@Component({
  selector: 'app-my-newsletters-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    LucideIconComponent,
    KyntusPageHeaderComponent,
    NewsletterReaderComponent,
    KyAuthMediaImgComponent,
  ],
  templateUrl: './my-newsletters-page.component.html',
  styleUrls: ['./my-newsletters-page.component.css']
})
export class MyNewslettersPageComponent implements OnInit {
  private readonly newsletterSvc = inject(NewsletterService);

  readonly icons = { search: Search, loader: Loader2, inbox: Inbox, mail: Mail, mailOpen: MailOpen };

  myNewsletters: EmployeeNewsletter[] = [];
  filteredNewsletters: EmployeeNewsletter[] = [];
  selectedNewsletter: EmployeeNewsletter | null = null;
  searchTerm = '';
  readFilter: 'all' | 'unread' | 'read' = 'all';
  loading = false;
  error: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.newsletterSvc.getMyNewsletters().subscribe({
      next: list => {
        this.myNewsletters = list;
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.error = 'Impossible de charger les communications.';
        this.loading = false;
      }
    });
  }

  applyFilters(): void {
    const q = this.searchTerm.trim().toLowerCase();
    this.filteredNewsletters = this.myNewsletters.filter(n => {
      if (this.readFilter === 'unread' && n.isRead) return false;
      if (this.readFilter === 'read' && !n.isRead) return false;
      if (!q) return true;
      return (
        n.newsletterTitle.toLowerCase().includes(q) ||
        n.newsletterSubject.toLowerCase().includes(q) ||
        n.campaignName.toLowerCase().includes(q)
      );
    });
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.readFilter = 'all';
    this.applyFilters();
  }

  getReadCount(): number {
    return this.myNewsletters.filter(n => n.isRead).length;
  }

  getUnreadCount(): number {
    return this.myNewsletters.filter(n => !n.isRead).length;
  }

  openNewsletter(nl: EmployeeNewsletter): void {
    this.selectedNewsletter = nl;
    if (!nl.isRead) {
      this.newsletterSvc.markAsRead(nl.analyticsId).subscribe({
        next: () => {
          nl.isRead = true;
          nl.readAt = new Date().toISOString();
        }
      });
    }
  }

  closeNewsletter(): void {
    this.selectedNewsletter = null;
  }
}
