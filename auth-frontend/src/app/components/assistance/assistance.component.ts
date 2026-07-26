import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { KyntusThemeService } from '../../core/kyntus-theme.service';
import { brandLogoSrc } from '../../core/brand-logo';
import { ThemeToggleButtonComponent } from '../../core/theme-toggle-button.component';

interface FaqItem {
  question: string;
  answer: string;
}

interface ModuleGuide {
  title: string;
  description: string;
}

@Component({
  selector: 'app-assistance',
  standalone: true,
  imports: [CommonModule, RouterModule, ThemeToggleButtonComponent],
  templateUrl: './assistance.component.html',
  styleUrls: ['./assistance.component.css'],
})
export class AssistanceComponent implements OnInit {
  private readonly title = inject(Title);
  readonly theme = inject(KyntusThemeService);
  readonly openFaq = signal<number | null>(0);

  readonly modules: ModuleGuide[] = [
    {
      title: 'Planning',
      description: 'Consultez vos plannings et suivez les demandes de changement.',
    },
    {
      title: 'Formation',
      description: 'Parcours, sessions et documents associés à votre formation.',
    },
    {
      title: 'Congés',
      description: 'Déposez une demande d’absence et consultez vos soldes.',
    },
    {
      title: 'Documents',
      description: 'Consultez et déposez la documentation ressources humaines.',
    },
    {
      title: 'Primes',
      description: 'Suivi des primes selon votre profil et vos habilitations.',
    },
    {
      title: 'Parrainage',
      description: 'Suivi des parcours de parrainage qui vous concernent.',
    },
  ];

  readonly faq: FaqItem[] = [
    {
      question: 'Je ne vois pas un module',
      answer:
        'Les modules s’affichent selon vos habilitations. Si un domaine métier ne figure pas dans le menu après connexion, il n’est pas associé à votre rôle. Ouvrez une réclamation dans le portail pour faire vérifier vos droits.',
    },
    {
      question: 'Ma demande ou mon congé est en attente',
      answer:
        'Les validations suivent un circuit (manager, RH, etc.). Consultez le statut dans le module concerné. En cas de délai anormal, ouvrez une réclamation en précisant le type de demande et la date.',
    },
    {
      question: 'Qui traite les réclamations ?',
      answer:
        'Les équipes RH et les profils de gestion compétents traitent les demandes dans l’espace de gestion. Vous suivez l’avancement depuis « Mes réclamations » une fois connecté.',
    },
    {
      question: 'Quelle différence entre réclamation et proposition ?',
      answer:
        'Une réclamation signale un dysfonctionnement ou une difficulté à résoudre. Une proposition d’amélioration suggère une évolution du processus ou de l’outil, sans urgence de correction.',
    },
    {
      question: 'Comment renouveler ma session ou mon mot de passe ?',
      answer:
        'Déconnectez-vous puis reconnectez-vous via l’écran d’authentification. Aucune demande de mot de passe n’est traitée par courriel : adressez-vous à votre responsable RH via le portail après connexion.',
    },
  ];

  get logoSrc(): string {
    return brandLogoSrc(this.theme.theme());
  }

  ngOnInit(): void {
    this.title.setTitle('Assistance — MyKyntus');
  }

  toggleFaq(index: number): void {
    this.openFaq.update((cur) => (cur === index ? null : index));
  }
}
