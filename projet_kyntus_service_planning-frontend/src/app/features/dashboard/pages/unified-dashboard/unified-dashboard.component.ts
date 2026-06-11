import { Component, OnInit, ViewEncapsulation, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { LayoutDashboard, Users, Calendar, FileText, MessageSquare, GraduationCap } from 'lucide';
import { Microservice } from '../../../../core/navigation/microservices.config';
import { NavigationMenuService } from '../../../../core/navigation/navigation-menu.service';
import { AuthService } from '../../../../core/services/auth.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';

type QuickLink = { label: string; route: string; icon: typeof LayoutDashboard };

@Component({
  selector: 'app-unified-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, LucideIconComponent],
  templateUrl: './unified-dashboard.component.html',
  styleUrls: ['./unified-dashboard.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class UnifiedDashboardComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly menuService = inject(NavigationMenuService);

  role = '';
  username = '';
  groups: Microservice[] = [];
  quickLinks: QuickLink[] = [];
  dashboardTitle = 'Tableau de bord';
  dashboardSubtitle = '';

  readonly icons = {
    dashboard: LayoutDashboard,
    users: Users,
    calendar: Calendar,
    contracts: FileText,
    reclamations: MessageSquare,
    formations: GraduationCap,
  };

  private iconCache = new Map<string, SafeHtml>();

  ngOnInit(): void {
    let user: { username?: string; role?: string } | null = null;
    try {
      user = JSON.parse(localStorage.getItem('user') || 'null');
    } catch {
      user = null;
    }
    this.username = user?.username || 'Utilisateur';
    this.role = (this.auth.getRole() || user?.role || '').trim();
    this.groups = this.menuService.buildVisibleGroups(this.role);
    this.buildRoleDashboard();
  }

  private buildRoleDashboard(): void {
    const r = this.role;
    const adminRh = ['Admin', 'RH'].includes(r);
    const managerLike = ['Manager', 'Coach', 'RP', 'Equipe_Formation'].includes(r);
    const employeeLike = ['Employee', 'Pilote', 'Audit'].includes(r);

    if (r === 'Superviseur') {
      this.dashboardTitle = 'Espace superviseur';
      this.dashboardSubtitle = 'Saisie PRIME, validation et pilotage de cellule';
      this.quickLinks = [
        { label: 'Module PRIME', route: '/prime', icon: this.icons.dashboard },
        { label: 'Mes réclamations', route: '/reclamations', icon: this.icons.reclamations },
        { label: 'Mes congés', route: '/mes-conges', icon: this.icons.calendar },
      ];
    } else if (adminRh) {
      this.dashboardTitle = 'Pilotage RH';
      this.dashboardSubtitle = 'Vue d’ensemble organisation, contrats et planification';
      this.quickLinks = [
        { label: 'Employés', route: '/users', icon: this.icons.users },
        { label: 'Contrats', route: '/contracts', icon: this.icons.contracts },
        { label: 'Plannings', route: '/planning', icon: this.icons.calendar },
        { label: 'Réclamations', route: '/reclamations-admin', icon: this.icons.reclamations },
        { label: 'Mes newsletters', route: '/mes-newsletters', icon: this.icons.reclamations },
      ];
    } else if (managerLike) {
      this.dashboardTitle = 'Espace manager';
      this.dashboardSubtitle = 'Suivi équipe, congés et formations';
      this.quickLinks = [
        { label: 'Plannings', route: '/planning', icon: this.icons.calendar },
        { label: 'Planning Équipe', route: '/planning/equipe', icon: this.icons.calendar },
        { label: 'Congés', route: '/conge-gestion', icon: this.icons.calendar },
        { label: 'Réclamations', route: '/reclamations-admin', icon: this.icons.reclamations },
        { label: 'Formations', route: '/formations', icon: this.icons.formations },
        { label: 'Mes newsletters', route: '/mes-newsletters', icon: this.icons.reclamations },
      ];
    } else if (employeeLike) {
      this.dashboardTitle = 'Mon espace';
      this.dashboardSubtitle = 'Planning, congés et formations personnelles';
      this.quickLinks = [
        { label: 'Mon planning', route: '/planning', icon: this.icons.calendar },
        { label: 'Mes congés', route: '/mes-conges', icon: this.icons.calendar },
        { label: 'Mes formations', route: '/mes-formations', icon: this.icons.formations },
        { label: 'Réclamations', route: '/reclamations', icon: this.icons.reclamations },
        { label: 'Mes newsletters', route: '/mes-newsletters', icon: this.icons.reclamations },
      ];
    } else {
      this.dashboardTitle = 'Tableau de bord';
      this.dashboardSubtitle = 'Accédez aux microservices autorisés pour votre rôle';
      this.quickLinks = [
        { label: 'Accueil modules', route: '/home', icon: this.icons.dashboard },
      ];
    }
  }

  open(g: Microservice): void {
    const first = g.children[0];
    if (!first) return;
    if (first.externalUrl) {
      window.open(first.externalUrl, '_blank');
      return;
    }
    if (first.route) {
      void this.router.navigateByUrl(first.route);
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
}
