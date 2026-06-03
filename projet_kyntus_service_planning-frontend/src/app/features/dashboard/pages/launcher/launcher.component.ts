import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MICROSERVICES, Microservice } from '../../../../core/navigation/microservices.config';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-launcher',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './launcher.component.html',
  styleUrls: ['./launcher.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class LauncherComponent implements OnInit {
  role = '';
  username = '';
  groups: Microservice[] = [];

  private iconCache = new Map<string, SafeHtml>();

  constructor(
    private router: Router,
    private auth: AuthService,
    private sanitizer: DomSanitizer,
  ) {}

  ngOnInit(): void {
    let user: any = null;
    try { user = JSON.parse(localStorage.getItem('user') || 'null'); } catch { /* ignore */ }
    this.username = user?.username || 'Utilisateur';
    this.role = (this.auth.getRole() || user?.role || '').trim();
    this.groups = this.buildVisibleGroups();
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

  open(g: Microservice): void {
    const first = g.children[0];
    if (!first) return;
    if (first.externalUrl) { window.open(first.externalUrl, '_blank'); return; }
    if (first.route) void this.router.navigateByUrl(first.route);
  }

  icon(svg: string): SafeHtml {
    let cached = this.iconCache.get(svg);
    if (!cached) {
      cached = this.sanitizer.bypassSecurityTrustHtml(svg);
      this.iconCache.set(svg, cached);
    }
    return cached;
  }
}
