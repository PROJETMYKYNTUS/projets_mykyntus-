import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { KyntusThemeService } from '../../core/kyntus-theme.service';
import { brandLogoSrc } from '../../core/brand-logo';
import { ThemeToggleButtonComponent } from '../../core/theme-toggle-button.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, ThemeToggleButtonComponent],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css'],
})
export class HomeComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly title = inject(Title);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly theme = inject(KyntusThemeService);
  private observer?: IntersectionObserver;

  navScrolled = false;
  menuOpen = false;
  ready = false;

  readonly groupUrl = 'https://kyntus.com/';
  readonly year = new Date().getFullYear();

  get logoSrc(): string {
    return brandLogoSrc(this.theme.theme());
  }

  ngOnInit(): void {
    this.title.setTitle('MyKyntus — Kyntus Morocco');
    if (isPlatformBrowser(this.platformId)) {
      requestAnimationFrame(() => (this.ready = true));
    }
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            this.observer?.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -8% 0px' }
    );
    this.host.nativeElement.querySelectorAll('[data-reveal]').forEach((el: Element) => {
      this.observer?.observe(el);
    });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  @HostListener('window:scroll')
  onScroll(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.navScrolled = window.scrollY > 16;
  }

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  scrollTo(id: string, event?: Event): void {
    event?.preventDefault();
    this.closeMenu();
    if (!isPlatformBrowser(this.platformId)) return;
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
