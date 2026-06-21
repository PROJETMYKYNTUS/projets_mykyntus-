import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AllowanceApiService, AllowanceTypeDto } from '../../services/allowance-api.service';

@Component({
  selector: 'app-allowances-catalog-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h1 class="text-xl font-semibold text-primary">Catalogue types de prime Support</h1>
        <button type="button" class="btn-primary text-sm" (click)="showForm.set(!showForm())">
          {{ showForm() ? 'Annuler' : 'Nouveau type' }}
        </button>
      </div>

      @if (showForm()) {
        <form class="card-navy p-4 space-y-3 max-w-xl" (ngSubmit)="create()">
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="block text-sm">
              Code
              <input class="input w-full mt-1" [(ngModel)]="formCode" name="code" required />
            </label>
            <label class="block text-sm">
              Libellé
              <input class="input w-full mt-1" [(ngModel)]="formLabel" name="label" required />
            </label>
            <label class="block text-sm">
              Catégorie
              <input class="input w-full mt-1" [(ngModel)]="formCategory" name="category" required />
            </label>
            <label class="block text-sm">
              Montant par défaut
              <input type="number" class="input w-full mt-1" [(ngModel)]="formDefaultAmount" name="defaultAmount" />
            </label>
            <label class="block text-sm">
              Minimum
              <input type="number" class="input w-full mt-1" [(ngModel)]="formMinAmount" name="minAmount" />
            </label>
            <label class="block text-sm">
              Maximum
              <input type="number" class="input w-full mt-1" [(ngModel)]="formMaxAmount" name="maxAmount" />
            </label>
          </div>
          <label class="flex items-center gap-2 text-sm">
            <input type="checkbox" [(ngModel)]="formRequiresJustification" name="justification" />
            Motif obligatoire
          </label>
          @if (error()) {
            <p class="text-sm text-rose-400">{{ error() }}</p>
          }
          @if (success()) {
            <p class="text-sm text-emerald-400">{{ success() }}</p>
          }
          <button type="submit" class="btn-primary text-sm" [disabled]="saving()">Créer le type</button>
        </form>
      }

      @if (loading()) {
        <p class="text-muted text-sm">Chargement…</p>
      } @else {
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          @for (t of types(); track t.id) {
            <div class="card-navy p-3 text-sm" [class.opacity-60]="!t.isActive">
              <div class="flex items-start justify-between gap-2">
                <p class="font-medium">{{ t.label }}</p>
                @if (t.isActive) {
                  <span class="text-xs text-emerald-400">Actif</span>
                } @else {
                  <span class="text-xs text-muted">Inactif</span>
                }
              </div>
              <p class="text-muted">{{ t.code }} · {{ t.category }}</p>
              @if (t.defaultAmount) {
                <p class="text-xs mt-1">Défaut : {{ t.defaultAmount | number:'1.0-0' }} MAD</p>
              }
              @if (t.maxAmount) {
                <p class="text-xs">Plafond : {{ t.maxAmount | number:'1.0-0' }} MAD</p>
              }
              @if (t.requiresJustification) {
                <p class="text-xs text-amber-400 mt-1">Motif obligatoire</p>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesCatalogPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly showForm = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly types = signal<AllowanceTypeDto[]>([]);

  formCode = '';
  formLabel = '';
  formCategory = 'Support';
  formDefaultAmount: number | null = null;
  formMinAmount: number | null = null;
  formMaxAmount: number | null = null;
  formRequiresJustification = false;

  ngOnInit(): void {
    void this.load();
  }

  async create(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await this.api.createType({
        code: this.formCode.trim(),
        label: this.formLabel.trim(),
        category: this.formCategory.trim(),
        defaultAmount: this.formDefaultAmount ?? undefined,
        minAmount: this.formMinAmount ?? undefined,
        maxAmount: this.formMaxAmount ?? undefined,
        requiresJustification: this.formRequiresJustification,
        applicableDepartmentKinds: 'Support',
      });
      this.success.set('Type créé avec succès.');
      this.showForm.set(false);
      this.formCode = '';
      this.formLabel = '';
      await this.load();
    } catch (e: unknown) {
      this.error.set(e instanceof Error ? e.message : 'Erreur lors de la création.');
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.types.set(await this.api.listTypes());
    } finally {
      this.loading.set(false);
    }
  }
}
