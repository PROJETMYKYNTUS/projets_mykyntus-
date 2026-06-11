import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FileUp, ArrowRight, X } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { ReferralService } from '../../services/referral.service';
import { AdminService } from '../../services/admin.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import type { ReferralRuleCatalogItem } from '../../models/referral.model';

const OTHER_POST_VALUE = '__OTHER__';

const MAX_CV_BYTES = 10 * 1024 * 1024;
const ALLOWED_CV_TYPES = new Set([
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
]);
const ALLOWED_CV_EXT = /\.(pdf|doc|docx)$/i;

@Component({
  selector: 'app-pilote-submit-page',
  standalone: true,
  imports: [FormsModule, NgClass, LucideIconComponent],
  template: `
    <div class="space-y-4">
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold text-slate-50">
            Soumettre un parrainage
          </h2>
          <p class="text-sm text-slate-500">
            Recommandez un talent pour rejoindre l'équipe.
          </p>
        </div>
      </div>

      @if (done()) {
        <div class="rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-200">
          Parrainage soumis avec succès.
        </div>
      }
      @if (error()) {
        <div class="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
          {{ error() }}
        </div>
      }

      <form (ngSubmit)="submit()" class="grid gap-4 lg:grid-cols-3">
        <div class="card-navy p-4 lg:col-span-2 space-y-4">
          <div class="grid gap-4 md:grid-cols-2">
            <div class="space-y-1.5">
              <label class="text-xs text-slate-400">Nom du candidat</label>
              <input
                required
                class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                placeholder="Ex : Thomas Dupont"
                [(ngModel)]="form.candidateName"
                name="candidateName"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-slate-400">E-mail du candidat</label>
              <input
                required
                type="email"
                class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                placeholder="Ex : thomas.dupont@example.com"
                [(ngModel)]="form.candidateEmail"
                name="candidateEmail"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-slate-400">Téléphone</label>
              <input
                required
                class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                placeholder="+33 6 ..."
                [(ngModel)]="form.candidatePhone"
                name="candidatePhone"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-slate-400">Poste ciblé</label>
              <select
                required
                class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                [(ngModel)]="selectedRuleId"
                name="selectedRuleId"
              >
                <option value="" disabled>Sélectionnez un poste</option>
                @for (item of catalog(); track item.ruleId) {
                  <option [value]="item.ruleId">
                    {{ item.target }} — {{ item.value }} DH ({{ item.minDurationMonths }} mois)
                  </option>
                }
                <option [value]="otherPostValue">Autre poste</option>
              </select>
              @if (!isOtherPost && previewLabel) {
                <p class="mt-2 text-xs text-cyan-200/90 rounded-lg border border-cyan-500/20 bg-cyan-500/5 px-3 py-2">
                  {{ previewLabel }}
                </p>
              }
            </div>
            @if (isOtherPost) {
              <div class="space-y-1.5 md:col-span-2">
                <label class="text-xs text-slate-400">Poste</label>
                <input
                  required
                  class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                  placeholder="Ex : Développeur Full-Stack Senior"
                  [(ngModel)]="customPosition"
                  name="customPosition"
                />
                @if (previewLabel) {
                  <p class="mt-2 text-xs text-cyan-200/90 rounded-lg border border-cyan-500/20 bg-cyan-500/5 px-3 py-2">
                    {{ previewLabel }}
                  </p>
                }
              </div>
            }
          </div>

          <div class="space-y-1.5">
            <label class="text-xs text-slate-400">Projet / contexte</label>
            <input
              class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
              placeholder="Ex : Portail Collaborateur, Digital Factory..."
              [(ngModel)]="form.project"
              name="project"
            />
          </div>

          <div class="space-y-1.5">
            <label class="text-xs text-slate-400">
              Notes / commentaires
            </label>
            <textarea
              class="w-full min-h-[80px] rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
              placeholder="Partagez les points forts, la motivation du candidat, le contexte..."
              [(ngModel)]="form.notes"
              name="notes"
            ></textarea>
          </div>
        </div>

        <div class="space-y-4">
          <div class="card-navy p-4 flex flex-col gap-3">
            <p class="text-xs font-medium text-slate-400">
              CV du candidat (PDF, DOCX) <span class="text-rose-300">* obligatoire</span>
            </p>
            <input
              #fileInput
              type="file"
              class="hidden"
              accept=".pdf,.doc,.docx,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
              (change)="onFileSelected($event)"
            />
            <div
              class="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed px-4 py-6 text-center transition-colors"
              [ngClass]="
                cvMissingHighlight()
                  ? 'border-rose-500/60 bg-rose-500/5'
                  : dragOver()
                    ? 'border-soft-blue bg-navy-800/60'
                    : 'border-navy-800 bg-navy-900/60'
              "
              (click)="fileInput.click()"
              (dragover)="onDragOver($event)"
              (dragleave)="dragOver.set(false)"
              (drop)="onDrop($event)"
            >
              <app-lucide-icon [icon]="fileUpIcon" className="h-6 w-6 text-soft-blue mb-1" />
              @if (cvFile()) {
                <p class="text-xs text-slate-200 font-medium">{{ cvFile()!.name }}</p>
                <p class="text-[11px] text-slate-500">{{ formatSize(cvFile()!.size) }}</p>
                <button
                  type="button"
                  (click)="clearFile($event)"
                  class="inline-flex items-center gap-1 text-[11px] text-rose-300 hover:text-rose-200"
                >
                  <app-lucide-icon [icon]="xIcon" className="h-3 w-3" />
                  Retirer
                </button>
              } @else {
                <p class="text-xs text-slate-300">
                  Glissez-déposez le CV ici ou
                  <span class="text-soft-blue font-medium">
                    sélectionnez un fichier
                  </span>
                </p>
                <p class="text-[11px] text-slate-500">
                  Taille maximale 10 Mo • 1 fichier • obligatoire
                </p>
              }
            </div>
            @if (cvMissingHighlight()) {
              <p class="text-xs text-rose-300">Le CV du candidat est obligatoire pour soumettre.</p>
            }
          </div>

          <button
            type="submit"
            [disabled]="done() || busy() || !cvFile()"
            class="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm hover:bg-blue-500 transition-colors disabled:opacity-60"
          >
            @if (busy()) {
              Envoi en cours…
            } @else {
              Soumettre le parrainage
              <app-lucide-icon [icon]="arrowRightIcon" className="h-4 w-4" />
            }
          </button>

          <div class="card-navy p-3 text-[11px] text-slate-400 space-y-1">
            <p class="font-medium text-slate-200">
              Rappel des règles du programme
            </p>
            <ul class="list-disc list-inside space-y-0.5">
              <li>Le candidat ne doit pas déjà être en process actif.</li>
              <li>
                La prime est versée après validation de la période d'essai.
              </li>
              <li>Le CV du candidat (PDF ou DOCX) est obligatoire.</li>
              <li>
                Les informations partagées doivent être exactes et complètes.
              </li>
            </ul>
          </div>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PiloteSubmitPageComponent implements OnInit {
  private readonly referrals = inject(ReferralService);
  private readonly admin = inject(AdminService);
  private readonly role = inject(ParrainageRoleService);
  readonly otherPostValue = OTHER_POST_VALUE;
  readonly catalog = signal<ReferralRuleCatalogItem[]>([]);
  selectedRuleId = '';
  customPosition = '';
  readonly done = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly cvFile = signal<File | null>(null);
  readonly cvMissingHighlight = signal(false);
  readonly dragOver = signal(false);
  readonly fileUpIcon = FileUp;
  readonly arrowRightIcon = ArrowRight;
  readonly xIcon = X;
  form = { candidateName: '', candidateEmail: '', candidatePhone: '', project: '', notes: '' };

  get isOtherPost(): boolean {
    return this.selectedRuleId === OTHER_POST_VALUE;
  }

  get previewLabel(): string {
    if (this.isOtherPost) {
      const cfg = this.admin.getSystemConfig();
      return `Règle générale : ${cfg.defaultBonusAmount} DH — ancienneté minimale ${cfg.minDurationMonths} mois`;
    }
    if (!this.selectedRuleId) return '';
    const item = this.catalog().find((c) => c.ruleId === this.selectedRuleId);
    if (!item) return '';
    return `Prime poste : ${item.value} DH — ancienneté minimale ${item.minDurationMonths} mois`;
  }

  ngOnInit(): void {
    void this.loadCatalog();
  }

  private async loadCatalog(): Promise<void> {
    try {
      this.catalog.set(await this.referrals.getRulesCatalog());
    } catch {
      this.catalog.set([]);
    }
  }

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(true);
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(false);
    const file = e.dataTransfer?.files?.[0];
    if (file) this.setCvFile(file);
  }

  onFileSelected(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.setCvFile(file);
    input.value = '';
  }

  clearFile(e: Event): void {
    e.stopPropagation();
    this.cvFile.set(null);
    this.error.set(null);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} o`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} Ko`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`;
  }

  private setCvFile(file: File): void {
    const err = this.validateCv(file);
    if (err) {
      this.error.set(err);
      this.cvFile.set(null);
      return;
    }
    this.error.set(null);
    this.cvFile.set(file);
    this.cvMissingHighlight.set(false);
  }

  private validateCv(file: File): string | null {
    if (file.size > MAX_CV_BYTES) return 'Le fichier dépasse la taille maximale de 10 Mo.';
    const okType = ALLOWED_CV_TYPES.has(file.type) || ALLOWED_CV_EXT.test(file.name);
    if (!okType) return 'Format non autorisé. Utilisez PDF, DOC ou DOCX.';
    return null;
  }

  async submit(): Promise<void> {
    if (this.busy() || this.done()) return;
    const cv = this.cvFile();
    if (!cv) {
      this.cvMissingHighlight.set(true);
      this.error.set('Le CV du candidat est obligatoire.');
      return;
    }
    const err = this.validateCv(cv);
    if (err) {
      this.error.set(err);
      return;
    }
    const user = this.role.user();
    if (!this.selectedRuleId) {
      this.error.set('Sélectionnez un poste dans la liste.');
      return;
    }
    if (this.isOtherPost && !this.customPosition.trim()) {
      this.error.set('Précisez le poste.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.referrals.submitReferral({
        referrerId: user.id,
        referrerName: user.name,
        candidateName: this.form.candidateName,
        candidateEmail: this.form.candidateEmail,
        candidatePhone: this.form.candidatePhone,
        ruleId: this.isOtherPost ? undefined : this.selectedRuleId,
        position: this.isOtherPost ? this.customPosition.trim() : undefined,
        project: this.form.project || undefined,
        notes: this.form.notes.trim() || undefined,
        cvFile: cv,
      });
      this.done.set(true);
      this.cvFile.set(null);
    } catch (e) {
      let msg = 'Échec de la soumission du parrainage.';
      if (e instanceof HttpErrorResponse) {
        const body = e.error as { error?: string } | string | null;
        if (body && typeof body === 'object' && typeof body.error === 'string') msg = body.error;
        else if (typeof body === 'string' && body) msg = body;
        else if (e.message) msg = e.message;
      } else if (e instanceof Error) {
        msg = e.message;
      }
      this.error.set(msg);
    } finally {
      this.busy.set(false);
    }
  }
}
