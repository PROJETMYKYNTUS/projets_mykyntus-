import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { FormationDocumentChecklistItemDto } from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';

@Component({
  selector: 'app-formation-path-checklist',
  standalone: true,
  imports: [CommonModule, RouterLink, KyntusPageHeaderComponent],
  template: `
    <section class="ky-page-shell fpc-page">
      <app-kyntus-page-header
        title="Checklist documents"
        [subtitle]="employeeName() || 'Pièces à apporter en entreprise'"
      >
        <a
          actions
          class="ky-btn-secondary"
          [routerLink]="backPath()"
          [queryParams]="backQuery()"
        >
          {{ backLabel() }}
        </a>
      </app-kyntus-page-header>

      <div class="ky-card fpc-card">
        <p class="fpc-count">
          Reçus : <strong>{{ receivedCount() }}</strong> / {{ items().length }}
        </p>
        @for (item of items(); track item.id) {
          <label class="fpc-item">
            <span class="fpc-title">{{ item.title }}</span>
            <span class="fpc-item-meta">
              @if (item.isReceived && item.receivedAt) {
                <span class="fpc-date">{{ item.receivedAt | date: 'short' }}</span>
              }
              <input
                type="checkbox"
                [checked]="item.isReceived"
                (change)="toggle(item, $any($event.target).checked)"
              />
              <span class="fpc-state">{{ item.isReceived ? 'Reçu' : 'Manquant' }}</span>
            </span>
          </label>
        } @empty {
          <p class="fpc-muted">Aucun document configuré pour ce parcours.</p>
        }
        @if (error()) {
          <p class="fpc-error">{{ error() }}</p>
        }
      </div>

      <footer class="fpc-nav">
        <a class="ky-btn-secondary" [routerLink]="backPath()" [queryParams]="backQuery()">
          {{ backLabel() }}
        </a>
      </footer>
    </section>
  `,
  styles: [`
    .fpc-page { display: grid; gap: 0.85rem; }
    .fpc-card {
      padding: 1rem;
      display: grid;
      gap: 0.65rem;
    }
    .fpc-count {
      margin: 0;
      font-size: 0.85rem;
      color: var(--text-muted);
    }
    .fpc-item {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem 0.75rem;
      padding: 0.65rem 0.75rem;
      border: 1px solid color-mix(in srgb, var(--border-color) 80%, transparent);
      border-radius: var(--radius-card, 0.5rem);
      cursor: pointer;
    }
    .fpc-title { color: var(--text-primary); font-size: 0.9rem; }
    .fpc-item-meta {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      font-size: 0.8rem;
      color: var(--text-muted);
    }
    .fpc-date { font-size: 0.72rem; }
    .fpc-muted { margin: 0; color: var(--text-muted); }
    .fpc-error { margin: 0; color: #dc2626; font-size: 0.85rem; }
    .fpc-nav {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: flex-start;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationPathChecklistComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);

  readonly items = signal<FormationDocumentChecklistItemDto[]>([]);
  readonly employeeName = signal('');
  readonly error = signal<string | null>(null);
  readonly backPath = signal('/formations');
  readonly backQuery = signal<Record<string, string>>({ tab: 'initial' });
  readonly backLabel = signal('Retour liste');
  pathId = '';

  receivedCount = () => this.items().filter((i) => i.isReceived).length;

  ngOnInit(): void {
    this.pathId = this.route.snapshot.paramMap.get('pathId') ?? '';
    this.employeeName.set(this.route.snapshot.queryParamMap.get('name') ?? '');
    this.applyReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    void this.reload();
  }

  private applyReturnUrl(raw: string | null): void {
    const parsed = parseInternalFormationReturnUrl(raw);
    this.backPath.set(parsed.path);
    this.backQuery.set(parsed.query);
    this.backLabel.set(parsed.label);
  }

  private async reload(): Promise<void> {
    if (!this.pathId) return;
    this.items.set(await this.api.getPathChecklist(this.pathId));
  }

  async toggle(item: FormationDocumentChecklistItemDto, isReceived: boolean): Promise<void> {
    this.error.set(null);
    try {
      await this.api.updateChecklistItem(this.pathId, item.id, {
        isReceived,
        receivedBy: this.session.getStoredUser()?.username || 'RH',
      });
      await this.reload();
    } catch (e: any) {
      this.error.set(e?.message || 'Échec de la mise à jour');
      await this.reload();
    }
  }
}

function parseInternalFormationReturnUrl(raw: string | null): {
  path: string;
  query: Record<string, string>;
  label: string;
} {
  const fallback = {
    path: '/formations',
    query: { tab: 'initial' } as Record<string, string>,
    label: 'Retour liste',
  };

  if (!raw) return fallback;
  const trimmed = raw.trim();
  if (!trimmed.startsWith('/formations') || trimmed.startsWith('//') || trimmed.includes('://')) {
    return fallback;
  }

  const [pathPart, queryPart] = trimmed.split('?');
  const query: Record<string, string> = {};
  if (queryPart) {
    for (const pair of queryPart.split('&')) {
      if (!pair) continue;
      const eq = pair.indexOf('=');
      const key = decodeURIComponent(eq >= 0 ? pair.slice(0, eq) : pair);
      const value = decodeURIComponent(eq >= 0 ? pair.slice(eq + 1) : '');
      if (key) query[key] = value;
    }
  }

  return {
    path: pathPart || '/formations',
    query,
    label: labelForFormationPath(pathPart),
  };
}

function labelForFormationPath(path: string): string {
  if (path.startsWith('/formations/passage-production')) return 'Retour Passage production';
  if (path.startsWith('/formations/dashboard')) return 'Retour tableau de bord';
  if (path === '/formations' || path.startsWith('/formations?')) return 'Retour liste';
  return 'Retour';
}
