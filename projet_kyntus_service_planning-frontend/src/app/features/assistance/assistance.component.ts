import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { KyntusPageHeaderComponent } from '../../shared/components/ui/kyntus-page-header.component';
import { KyntusSessionService } from '../../core/session/kyntus-session.service';
import { roleNamesMatch } from '../../core/org/org-role-assignment';

interface AssistanceLink {
  title: string;
  description: string;
  route: string;
  roles?: string[];
}

interface ModuleGuide {
  title: string;
  description: string;
  route: string;
  roles?: string[];
}

interface FaqItem {
  question: string;
  answer: string;
}

@Component({
  selector: 'app-assistance',
  standalone: true,
  imports: [CommonModule, RouterLink, KyntusPageHeaderComponent],
  templateUrl: './assistance.component.html',
  styleUrl: './assistance.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssistanceComponent {
  private readonly session = inject(KyntusSessionService);

  readonly openFaq = signal<number | null>(0);

  private readonly role = computed(() => this.session.getRole() || '');

  private readonly quickLinksAll: AssistanceLink[] = [
    {
      title: 'Créer ou suivre une demande',
      description: 'Réclamations et propositions d’amélioration, avec suivi du traitement.',
      route: '/reclamations',
      roles: [
        'Employee',
        'RH',
        'Manager',
        'Coach',
        'RP',
        'Admin',
        'Audit',
        'Equipe_Formation',
        'Equipe formation',
        'Superviseur',
        'Pilote',
        'Formateur',
      ],
    },
    {
      title: 'Gestion des réclamations',
      description: 'Traiter et clôturer les demandes remontées par les collaborateurs.',
      route: '/reclamations-admin',
      roles: ['RH', 'Manager', 'RP', 'Admin', 'Audit'],
    },
  ];

  private readonly modulesAll: ModuleGuide[] = [
    {
      title: 'Planning',
      description: 'Consultez vos plannings et suivez les demandes de changement.',
      route: '/mes-plannings',
    },
    {
      title: 'Formation',
      description: 'Parcours, sessions et documents associés à votre formation.',
      route: '/mes-formations',
    },
    {
      title: 'Congés',
      description: 'Déposez une demande de congé et consultez vos soldes.',
      route: '/mes-conges',
    },
    {
      title: 'Documents',
      description: 'Consultez et déposez la documentation ressources humaines.',
      route: '/documentation',
    },
    {
      title: 'Primes',
      description: 'Suivi des primes selon votre profil et vos habilitations.',
      route: '/prime',
    },
    {
      title: 'Parrainage',
      description: 'Suivi des parcours de parrainage qui vous concernent.',
      route: '/parrainage',
    },
  ];

  readonly faq: FaqItem[] = [
    {
      question: 'Je ne vois pas un module',
      answer:
        'Les modules s’affichent selon vos habilitations. Si un domaine métier ne figure pas dans le menu, il n’est pas associé à votre rôle. Adressez une demande via Réclamations pour faire vérifier vos droits.',
    },
    {
      question: 'Ma demande ou mon congé est en attente',
      answer:
        'Les validations suivent un circuit (manager, RH, etc.). Consultez le statut dans le module concerné. En cas de délai anormal, ouvrez une réclamation en précisant le type de demande et la date.',
    },
    {
      question: 'Qui traite les réclamations ?',
      answer:
        'Les équipes RH et les profils de gestion compétents traitent les demandes dans l’espace de gestion des réclamations. Vous suivez l’avancement depuis « Mes réclamations ».',
    },
    {
      question: 'Quelle différence entre réclamation et proposition ?',
      answer:
        'Une réclamation signale un dysfonctionnement ou une difficulté à résoudre. Une proposition d’amélioration suggère une évolution du processus ou de l’outil, sans urgence de correction.',
    },
  ];

  readonly quickLinks = computed(() =>
    this.quickLinksAll.filter((l) => this.isAllowed(l.roles)),
  );

  readonly modules = computed(() =>
    this.modulesAll.filter((m) => this.isAllowed(m.roles)),
  );

  toggleFaq(index: number): void {
    this.openFaq.update((cur) => (cur === index ? null : index));
  }

  private isAllowed(roles?: string[]): boolean {
    if (!roles?.length) return true;
    const role = this.role();
    return roles.some((r) => roleNamesMatch(r, role));
  }
}
