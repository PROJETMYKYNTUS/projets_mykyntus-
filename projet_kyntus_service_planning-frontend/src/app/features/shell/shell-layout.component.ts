import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { filter } from 'rxjs/operators';
import { MICROSERVICES, Microservice, MenuItem } from '../../core/navigation/microservices.config';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-shell-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shell-layout.component.html',
  styleUrls: ['./shell-layout.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class ShellLayoutComponent implements OnInit {
  currentUser: any = null;
  role = '';
  sidebarOpen = false;

  readonly homeIcon =
    '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>';

  /** Microservices visibles pour le rôle courant, avec leurs enfants filtrés. */
  groups: Microservice[] = [];
  /** Groupes dépliés (ids). */
  openGroups = new Set<string>();

  private iconCache = new Map<string, SafeHtml>();

  constructor(
    private router: Router,
    private auth: AuthService,
    private sanitizer: DomSanitizer,
  ) {}

  ngOnInit(): void {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try { this.currentUser = JSON.parse(userStr); } catch { this.currentUser = null; }
    }
    this.role = (this.auth.getRole() || this.currentUser?.role || '').trim();

    if (!localStorage.getItem('token')) {
      window.location.href = 'http://localhost:4201/login';
      return;
    }

    this.groups = this.buildVisibleGroups();
    this.openGroupForUrl(this.router.url);

    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.openGroupForUrl(e.urlAfterRedirects));
  }

  private roleAllowed(roles?: string[]): boolean {
    if (!roles || roles.length === 0) return true;
    const r = this.role.toLowerCase();
    return roles.some((x) => x.toLowerCase() === r);
  }

  private buildVisibleGroups(): Microservice[] {
    return MICROSERVICES
      .map((g) => ({ ...g, children: g.children.filter((c) => this.roleAllowed(c.roles)) }))
      .filter((g) => this.roleAllowed(g.roles) && g.children.length > 0);
  }

  private openGroupForUrl(url: string): void {
    for (const g of this.groups) {
      if (g.children.some((c) => c.route && url.startsWith(c.route))) {
        this.openGroups.add(g.id);
      }
    }
  }

  toggleGroup(id: string): void {
    if (this.openGroups.has(id)) this.openGroups.delete(id);
    else this.openGroups.add(id);
  }

  isOpen(id: string): boolean {
    return this.openGroups.has(id);
  }

  isItemActive(item: MenuItem): boolean {
    return !!item.route && this.router.url.startsWith(item.route);
  }

  onItemClick(item: MenuItem): void {
    this.sidebarOpen = false;
    if (item.externalUrl) {
      window.open(item.externalUrl, '_blank');
      return;
    }
    if (item.route) {
      void this.router.navigateByUrl(item.route);
    }
  }

  icon(svg: string): SafeHtml {
    let cached = this.iconCache.get(svg);
    if (!cached) {
      cached = this.sanitizer.bypassSecurityTrustHtml(svg);
      this.iconCache.set(svg, cached);
    }
    return cached;
  }

  get userInitials(): string {
    const name: string = this.currentUser?.username || 'KY';
    return name.substring(0, 2).toUpperCase();
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  logout(): void {
    localStorage.clear();
    window.location.href = 'http://localhost:4201/login';
  }
}
