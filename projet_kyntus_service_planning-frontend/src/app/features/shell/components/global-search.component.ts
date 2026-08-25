import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { Search, X } from 'lucide';

import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import {
  GlobalSearchService,
  type GlobalSearchGroup,
  type GlobalSearchResult,
} from '../../../core/search/global-search.service';
import { ParrainageNavService } from '../../parrainage/state/parrainage-nav.service';
import { NavigationActionsService } from '../../../core/navigation/navigation-actions.service';

@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  template: `
    <div class="gs-root" #root>
      <div class="gs-field" [class.gs-field--open]="open()">
        <app-lucide-icon [icon]="icons.search" className="gs-field-icon" />
        <input
          #input
          type="text"
          class="gs-input"
          placeholder="Rechercher (employés, contrats, parrainage…)"
          autocomplete="off"
          [ngModel]="term"
          (ngModelChange)="onTermChange($event)"
          (focus)="onFocus()"
          (keydown)="onKeydown($event)"
          aria-label="Recherche globale"
        />
        @if (term) {
          <button type="button" class="gs-clear" (click)="clear()" aria-label="Effacer">
            <app-lucide-icon [icon]="icons.close" className="gs-clear-icon" />
          </button>
        }
      </div>

      @if (open()) {
        <div class="gs-panel" role="listbox">
          @if (loading()) {
            <div class="gs-state">Recherche…</div>
          } @else if (term.trim().length < 2) {
            <div class="gs-state">Tapez au moins 2 caractères pour rechercher.</div>
          } @else if (groups().length === 0) {
            <div class="gs-state">Aucun résultat pour « {{ term.trim() }} ».</div>
          } @else {
            @for (group of groups(); track group.type) {
              <div class="gs-group">
                <p class="gs-group-label">{{ group.label }}</p>
                @for (r of group.results; track r.type + r.id) {
                  <button
                    type="button"
                    class="gs-item"
                    [class.gs-item--active]="flatIndex(r) === activeIndex()"
                    (click)="select(r)"
                    (mouseenter)="activeIndex.set(flatIndex(r))"
                  >
                    <span class="gs-item-title">{{ r.title }}</span>
                    <span class="gs-item-sub">{{ r.subtitle }}</span>
                  </button>
                }
              </div>
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .gs-root {
      position: relative;
    }
    .gs-field {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 240px;
      max-width: 40vw;
      height: 38px;
      padding: 0 10px;
      border-radius: var(--radius-pill);
      background: var(--bg-input);
      border: 1px solid var(--border-color);
      transition: border-color 0.18s ease, box-shadow 0.18s ease, width 0.2s ease;
    }
    .gs-field:focus-within,
    .gs-field--open {
      border-color: color-mix(in srgb, var(--soft-blue) 55%, var(--border-color));
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--soft-blue) 16%, transparent);
    }
    :host ::ng-deep .gs-field-icon {
      width: 16px;
      height: 16px;
      color: var(--text-muted);
      flex-shrink: 0;
    }
    .gs-input {
      flex: 1;
      min-width: 0;
      background: transparent;
      border: none;
      outline: none;
      font-size: 13px;
      font-family: inherit;
      color: var(--text-primary);
    }
    .gs-input::placeholder {
      color: var(--text-muted);
    }
    .gs-clear {
      display: flex;
      align-items: center;
      justify-content: center;
      background: none;
      border: none;
      cursor: pointer;
      padding: 2px;
      border-radius: var(--radius-pill);
      color: var(--text-muted);
    }
    .gs-clear:hover {
      background: color-mix(in srgb, var(--text-muted) 15%, transparent);
    }
    :host ::ng-deep .gs-clear-icon {
      width: 14px;
      height: 14px;
    }
    .gs-panel {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: 360px;
      max-width: 80vw;
      max-height: 60vh;
      overflow-y: auto;
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-md);
      box-shadow: var(--shadow-3);
      padding: 6px;
      z-index: var(--z-dropdown);
      animation: gs-in 0.16s var(--ease-out);
    }
    @keyframes gs-in {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .gs-state {
      padding: 16px 12px;
      text-align: center;
      font-size: 13px;
      color: var(--text-muted);
    }
    .gs-group + .gs-group {
      margin-top: 4px;
      border-top: 1px solid var(--border-color);
      padding-top: 4px;
    }
    .gs-group-label {
      margin: 0;
      padding: 6px 10px 4px;
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--text-muted);
    }
    .gs-item {
      display: flex;
      flex-direction: column;
      gap: 1px;
      width: 100%;
      text-align: left;
      padding: 8px 10px;
      border: none;
      background: transparent;
      border-radius: var(--radius-md);
      cursor: pointer;
      font-family: inherit;
    }
    .gs-item--active,
    .gs-item:hover {
      background: color-mix(in srgb, var(--soft-blue) 12%, transparent);
    }
    .gs-item-title {
      font-size: 13px;
      font-weight: 600;
      color: var(--text-primary);
    }
    .gs-item-sub {
      font-size: 11.5px;
      color: var(--text-muted);
    }
    @media (max-width: 640px) {
      .gs-field {
        width: 140px;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GlobalSearchComponent implements OnInit, OnDestroy {
  private readonly searchService = inject(GlobalSearchService);
  private readonly router = inject(Router);
  private readonly parrainageNav = inject(ParrainageNavService);
  private readonly navActions = inject(NavigationActionsService);

  readonly icons = { search: Search, close: X };

  term = '';
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly groups = signal<GlobalSearchGroup[]>([]);
  readonly activeIndex = signal(-1);

  @ViewChild('root') private root?: ElementRef<HTMLElement>;
  @ViewChild('input') private input?: ElementRef<HTMLInputElement>;

  private readonly term$ = new Subject<string>();
  private sub = new Subscription();

  ngOnInit(): void {
    this.sub.add(
      this.term$
        .pipe(
          debounceTime(250),
          distinctUntilChanged(),
          switchMap((term) => {
            if (term.trim().length < 2) {
              this.loading.set(false);
              return this.searchService.search('');
            }
            this.loading.set(true);
            return this.searchService.search(term);
          }),
        )
        .subscribe({
          next: (groups) => {
            this.groups.set(groups);
            this.activeIndex.set(-1);
            this.loading.set(false);
          },
          error: () => {
            this.groups.set([]);
            this.loading.set(false);
          },
        }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  onTermChange(value: string): void {
    this.term = value;
    this.open.set(true);
    this.term$.next(value);
  }

  onFocus(): void {
    this.open.set(true);
  }

  clear(): void {
    this.term = '';
    this.groups.set([]);
    this.activeIndex.set(-1);
    this.input?.nativeElement.focus();
  }

  private get flat(): GlobalSearchResult[] {
    return this.groups().flatMap((g) => g.results);
  }

  flatIndex(result: GlobalSearchResult): number {
    return this.flat.findIndex((r) => r.type === result.type && r.id === result.id);
  }

  onKeydown(event: KeyboardEvent): void {
    const flat = this.flat;
    if (event.key === 'Escape') {
      this.open.set(false);
      this.input?.nativeElement.blur();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.open.set(true);
      if (flat.length) this.activeIndex.set((this.activeIndex() + 1) % flat.length);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (flat.length) this.activeIndex.set((this.activeIndex() - 1 + flat.length) % flat.length);
      return;
    }
    if (event.key === 'Enter') {
      const idx = this.activeIndex();
      const target = idx >= 0 ? flat[idx] : flat[0];
      if (target) {
        event.preventDefault();
        this.select(target);
      }
    }
  }

  select(result: GlobalSearchResult): void {
    this.open.set(false);
    this.term = '';
    this.groups.set([]);

    switch (result.type) {
      case 'employee':
        void this.router.navigate(['/users', result.id]);
        break;
      case 'contract':
        void this.router.navigate(['/contracts', result.id]);
        break;
      case 'parrainage':
        void this.router.navigateByUrl('/parrainage').then(() => {
          this.parrainageNav.openReferralDetails(result.id);
        });
        break;
      case 'document':
        void this.router.navigate(['/documentation', 'doc-gen'], {
          queryParams: { requestId: result.id },
        });
        break;
      case 'formation': {
        const kind = result.meta?.kind;
        const tab = kind === 'path' ? 'initial' : 'continue';
        void this.router.navigate(['/formations'], {
          queryParams: { tab, highlight: result.id },
        });
        break;
      }
      case 'conge': {
        const year = result.meta?.year;
        void this.router.navigate(['/conges/historique'], {
          queryParams: {
            demandeId: result.id,
            ...(year != null ? { annee: String(year) } : {}),
          },
        });
        break;
      }
      case 'prime': {
        const id = (result.id || '').trim();
        if (/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id)) {
          void this.router.navigate(['/users', id]);
        } else {
          void this.navActions.openPrimePath('/team-performance');
        }
        break;
      }
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.open()) return;
    const el = this.root?.nativeElement;
    if (el && !el.contains(event.target as Node)) {
      this.open.set(false);
    }
  }
}
