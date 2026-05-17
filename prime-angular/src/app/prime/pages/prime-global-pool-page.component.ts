import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Download, LoaderCircle, RefreshCw } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../components/prime-card.component';
import { RoleService } from '../state/role.service';
import type { Role } from '../models';
import {
  PrimeGlobalPoolApiService,
  type GlobalPoolInboxItemDto,
  type GlobalPoolInboxStepStatusDto,
} from '../services/prime-global-pool-api.service';
import { primeHttpErrorDetail } from '../lib/primeHttpErrorMessage';

@Component({
  selector: 'app-prime-global-pool-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, DatePipe],
  template: `
    <div class="p-8 space-y-6 min-h-full bg-navy-950">
      <div class="flex flex-wrap justify-between gap-4">
        <div>
          <h1 class="text-3xl font-bold text-slate-100 tracking-tight">Synthèse globale PRIME</h1>
          <p class="text-slate-400 mt-1 max-w-3xl text-sm leading-relaxed">
            Fichier Excel agrégé par période : validations selon le
            <span class="text-slate-300">workflow global</span> (admin) ou, en mode historique,
            <span class="text-slate-300">Manager</span> + <span class="text-slate-300">RH</span> puis
            <span class="text-slate-300">Comptabilité</span>. Généré depuis le brouillon superviseur.
          </p>
        </div>
        <button
          type="button"
          (click)="reload()"
          [disabled]="loading()"
          class="inline-flex items-center gap-2 rounded-lg border border-navy-700 bg-navy-900 px-4 py-2 text-sm text-slate-200 hover:bg-navy-800 disabled:opacity-50"
        >
          @if (loading()) {
            <app-lucide-icon [icon]="icons.loader" className="w-4 h-4 animate-spin" />
          } @else {
            <app-lucide-icon [icon]="icons.refresh" className="w-4 h-4" />
          }
          Actualiser
        </button>
      </div>

      <div class="rounded-lg border border-navy-800 bg-navy-900/50 px-4 py-3 text-sm text-slate-300">
        Connecté : <span class="text-slate-100 font-medium">{{ role.currentRole() }}</span> —
        {{ roleHint() }}
      </div>

      @if (error()) {
        <div class="rounded-lg border border-rose-500/40 bg-rose-950/40 px-4 py-3 text-sm text-rose-200">
          {{ error() }}
        </div>
      }

      @if (loading() && rows().length === 0) {
        <div class="py-16 flex justify-center">
          <app-lucide-icon [icon]="icons.loader" className="w-10 h-10 text-cyan-400 animate-spin" />
        </div>
      } @else {
        <app-prime-card title="File des brouillons avec synthèse globale" className="p-0">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-navy-800 text-left text-slate-400">
                  <th class="py-3 px-4">Période</th>
                  <th class="py-3 px-4">Cellule</th>
                  <th class="py-3 px-4">Fichier</th>
                  @if (displayStepColumns().length > 0) {
                    @for (col of displayStepColumns(); track col.stepId) {
                      <th class="py-3 px-4 whitespace-nowrap">
                        {{ col.sortOrder }} · {{ col.approverRole }}
                      </th>
                    }
                  } @else {
                    <th class="py-3 px-4">Manager</th>
                    <th class="py-3 px-4">RH</th>
                    <th class="py-3 px-4">Compta</th>
                  }
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                @if (rows().length === 0) {
                  <tr>
                    <td [attr.colspan]="tableColSpan()" class="py-10 text-center text-slate-500">
                      Aucune synthèse globale disponible. Un superviseur doit d’abord générer l’Excel (POST
                      global-pool/generate sur son brouillon).
                    </td>
                  </tr>
                } @else {
                  @for (r of rows(); track r.draftId) {
                    <tr class="border-b border-navy-800/80 hover:bg-navy-900/80">
                      <td class="py-3 px-4 font-mono text-slate-200">{{ r.period }}</td>
                      <td class="py-3 px-4 text-slate-300">{{ r.celluleId }}</td>
                      <td class="py-3 px-4 text-slate-400 text-xs">
                        @if (r.hasFile) {
                          {{ r.fileName || 'fichier' }}
                          <div class="text-slate-500 mt-0.5">{{ r.uploadedAt | date: 'short' }}</div>
                        } @else {
                          —
                        }
                      </td>
                      @if (displayStepColumns().length > 0) {
                        @for (st of r.stepStatuses ?? []; track st.stepId) {
                          <td class="py-3 px-4">
                            @if (st.approvedAt) {
                              <span class="text-emerald-400 text-xs">OK {{ st.approvedAt | date: 'short' }}</span>
                            } @else {
                              <span class="text-amber-400 text-xs">En attente</span>
                            }
                          </td>
                        }
                      } @else {
                        <td class="py-3 px-4">
                          @if (r.managerApprovedAt) {
                            <span class="text-emerald-400 text-xs">OK {{ r.managerApprovedAt | date: 'short' }}</span>
                          } @else {
                            <span class="text-amber-400 text-xs">En attente</span>
                          }
                        </td>
                        <td class="py-3 px-4">
                          @if (r.rhApprovedAt) {
                            <span class="text-emerald-400 text-xs">OK {{ r.rhApprovedAt | date: 'short' }}</span>
                          } @else {
                            <span class="text-amber-400 text-xs">En attente</span>
                          }
                        </td>
                        <td class="py-3 px-4">
                          @if (r.comptaAckAt) {
                            <span class="text-emerald-400 text-xs">OK {{ r.comptaAckAt | date: 'short' }}</span>
                          } @else if (r.poolDistributionUnlocked) {
                            <span class="text-amber-400 text-xs">En attente</span>
                          } @else {
                            <span class="text-slate-600 text-xs">—</span>
                          }
                        </td>
                      }
                      <td class="py-3 px-4 text-right whitespace-nowrap">
                        <div class="flex flex-wrap justify-end gap-2">
                          @if (r.hasFile) {
                            <button
                              type="button"
                              [disabled]="busyId() === r.draftId"
                              (click)="download(r)"
                              class="inline-flex items-center gap-1 rounded border border-navy-600 px-2 py-1 text-xs text-slate-200 hover:bg-navy-800 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.download" className="w-3.5 h-3.5" />
                              Télécharger
                            </button>
                          }
                          @if (showConfigurableApproveBtn(r)) {
                            <button
                              type="button"
                              [disabled]="busyId() === r.draftId"
                              (click)="doApproveStep(r)"
                              class="rounded bg-violet-600/90 px-2 py-1 text-xs text-white hover:bg-violet-500 disabled:opacity-50"
                            >
                              Valider l’étape
                            </button>
                          }
                          @if (showManagerBtn(r)) {
                            <button
                              type="button"
                              [disabled]="busyId() === r.draftId"
                              (click)="doManager(r)"
                              class="rounded bg-amber-600/90 px-2 py-1 text-xs text-white hover:bg-amber-500 disabled:opacity-50"
                            >
                              Valider Manager
                            </button>
                          }
                          @if (showRhBtn(r)) {
                            <button
                              type="button"
                              [disabled]="busyId() === r.draftId"
                              (click)="doRh(r)"
                              class="rounded bg-indigo-600/90 px-2 py-1 text-xs text-white hover:bg-indigo-500 disabled:opacity-50"
                            >
                              Valider RH
                            </button>
                          }
                          @if (showComptaBtn(r)) {
                            <button
                              type="button"
                              [disabled]="busyId() === r.draftId"
                              (click)="doCompta(r)"
                              class="rounded bg-cyan-600/90 px-2 py-1 text-xs text-white hover:bg-cyan-500 disabled:opacity-50"
                            >
                              Accusé compta
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeGlobalPoolPageComponent implements OnInit {
  readonly role = inject(RoleService);
  private readonly api = inject(PrimeGlobalPoolApiService);

  readonly icons = { loader: LoaderCircle, refresh: RefreshCw, download: Download };

  readonly rows = signal<GlobalPoolInboxItemDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);

  readonly currentRole = computed(() => this.role.currentRole() as Role);

  readonly displayStepColumns = computed((): GlobalPoolInboxStepStatusDto[] => {
    const r = this.rows().find((x) => x.stepStatuses && x.stepStatuses.length > 0);
    return r?.stepStatuses ?? [];
  });

  readonly tableColSpan = computed(() => 4 + Math.max(3, this.displayStepColumns().length));

  readonly usesConfigurableWorkflow = computed(() => this.displayStepColumns().length > 0);

  readonly roleHint = computed(() => {
    if (this.usesConfigurableWorkflow()) {
      return 'Workflow global piloté par la configuration (vagues et rôles). Validez lorsque le bouton « Valider l’étape » est affiché.';
    }
    switch (this.currentRole()) {
      case 'Manager':
        return 'Vous pouvez valider la ligne Manager lorsque le fichier est présent.';
      case 'RH':
        return 'Vous pouvez valider la ligne RH (indépendamment de l’ordre par rapport au Manager).';
      case 'Comptabilité':
        return 'L’accusé réception n’est disponible qu’après validations Manager et RH.';
      case 'Admin':
        return 'Vous pouvez enchaîner les trois étapes pour démonstration.';
      default:
        return 'Lecture de la file.';
    }
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const uid = this.role.currentUser().id;
    this.api.inbox(uid).subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(primeHttpErrorDetail(err) ?? 'Impossible de charger la file.');
        this.rows.set([]);
        this.loading.set(false);
      },
    });
  }

  showConfigurableApproveBtn(r: GlobalPoolInboxItemDto): boolean {
    return !!(r.suggestedApproveStepId && r.hasFile);
  }

  showManagerBtn(r: GlobalPoolInboxItemDto): boolean {
    if (this.displayStepColumns().length > 0) return false;
    const role = this.currentRole();
    if (!(role === 'Manager' || role === 'Admin')) return false;
    return r.hasFile && !r.managerApprovedAt;
  }

  showRhBtn(r: GlobalPoolInboxItemDto): boolean {
    if (this.displayStepColumns().length > 0) return false;
    const role = this.currentRole();
    if (!(role === 'RH' || role === 'Admin')) return false;
    return r.hasFile && !r.rhApprovedAt;
  }

  showComptaBtn(r: GlobalPoolInboxItemDto): boolean {
    if (this.displayStepColumns().length > 0) return false;
    const role = this.currentRole();
    if (!(role === 'Comptabilité' || role === 'Admin')) return false;
    return r.poolDistributionUnlocked && !r.comptaAckAt;
  }

  download(r: GlobalPoolInboxItemDto): void {
    const uid = this.role.currentUser().id;
    this.busyId.set(r.draftId);
    this.api.downloadExcel(r.draftId, uid).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = r.fileName?.trim() || `prime-global-${r.period}.xlsx`;
        a.click();
        URL.revokeObjectURL(url);
        this.busyId.set(null);
      },
      error: (err) => {
        this.error.set(primeHttpErrorDetail(err) ?? 'Téléchargement impossible.');
        this.busyId.set(null);
      },
    });
  }

  doApproveStep(r: GlobalPoolInboxItemDto): void {
    const stepId = r.suggestedApproveStepId;
    if (!stepId) return;
    const uid = this.role.currentUser().id;
    this.busyId.set(r.draftId);
    this.api
      .approveStep(r.draftId, { userId: uid, stepId, role: this.role.currentRole() })
      .subscribe({
        next: () => {
          this.busyId.set(null);
          this.reload();
        },
        error: (err) => {
          this.error.set(primeHttpErrorDetail(err) ?? 'Validation d’étape refusée.');
          this.busyId.set(null);
        },
      });
  }

  doManager(r: GlobalPoolInboxItemDto): void {
    const uid = this.role.currentUser().id;
    this.busyId.set(r.draftId);
    this.api.approveManager(r.draftId, uid).subscribe({
      next: () => {
        this.busyId.set(null);
        this.reload();
      },
      error: (err) => {
        this.error.set(primeHttpErrorDetail(err) ?? 'Validation Manager refusée.');
        this.busyId.set(null);
      },
    });
  }

  doRh(r: GlobalPoolInboxItemDto): void {
    const uid = this.role.currentUser().id;
    this.busyId.set(r.draftId);
    this.api.approveRh(r.draftId, uid).subscribe({
      next: () => {
        this.busyId.set(null);
        this.reload();
      },
      error: (err) => {
        this.error.set(primeHttpErrorDetail(err) ?? 'Validation RH refusée.');
        this.busyId.set(null);
      },
    });
  }

  doCompta(r: GlobalPoolInboxItemDto): void {
    const uid = this.role.currentUser().id;
    this.busyId.set(r.draftId);
    this.api.ackCompta(r.draftId, uid).subscribe({
      next: () => {
        this.busyId.set(null);
        this.reload();
      },
      error: (err) => {
        this.error.set(primeHttpErrorDetail(err) ?? 'Accusé compta refusé.');
        this.busyId.set(null);
      },
    });
  }
}
