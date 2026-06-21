import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AllowanceApiService, AllowanceTypeDto } from '../../services/allowance-api.service';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { PrimeCardComponent } from '../../components/prime-card.component';

@Component({
  selector: 'app-allowances-catalog-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowancesPageShellComponent, PrimeCardComponent],
  template: `
    <app-allowances-page-shell
      title="Types de prime Support"
      subtitle="Les managers utilisent ces types lors de la création de demandes."
      [error]="error()"
    >
      <div pageActions>
        <button type="button" class="btn-primary text-sm" (click)="showForm.set(!showForm())">
          {{ showForm() ? 'Annuler' : 'Ajouter un type' }}
        </button>
      </div>

      @if (showForm()) {
        <app-prime-card title="Nouveau type de prime">
          <form class="space-y-3 max-w-xl" (ngSubmit)="create()">
            <div class="grid gap-3 sm:grid-cols-2">
              <label class="block text-sm text-primary">
                Code
                <input class="doc-field w-full mt-1" [(ngModel)]="formCode" name="code" required />
              </label>
              <label class="block text-sm text-primary">
                Libellé
                <input class="doc-field w-full mt-1" [(ngModel)]="formLabel" name="label" required />
              </label>
              <label class="block text-sm text-primary">
                Catégorie
                <input class="doc-field w-full mt-1" [(ngModel)]="formCategory" name="category" required />
              </label>
              <label class="block text-sm text-primary">
                Montant par défaut
                <input type="number" class="doc-field w-full mt-1" [(ngModel)]="formDefaultAmount" name="defaultAmount" />
              </label>
              <label class="block text-sm text-primary">
                Minimum
                <input type="number" class="doc-field w-full mt-1" [(ngModel)]="formMinAmount" name="minAmount" />
              </label>
              <label class="block text-sm text-primary">
                Maximum
                <input type="number" class="doc-field w-full mt-1" [(ngModel)]="formMaxAmount" name="maxAmount" />
              </label>
            </div>
            <label class="flex items-center gap-2 text-sm text-primary">
              <input type="checkbox" [(ngModel)]="formRequiresJustification" name="justification" />
              Motif obligatoire
            </label>
            @if (formError()) {
              <p class="text-sm text-rose-400">{{ formError() }}</p>
            }
            @if (success()) {
              <p class="text-sm text-emerald-400">{{ success() }}</p>
            }
            <button type="submit" class="btn-primary text-sm" [disabled]="saving()">Créer le type</button>
          </form>
        </app-prime-card>
      }

      @if (loading()) {
        <div class="flex justify-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        </div>
      } @else if (types().length === 0) {
        <app-prime-card title="Aucun type configuré">
          <p class="text-sm text-muted mb-4">
            Ajoutez les types de prime que les managers pourront sélectionner lors de leurs demandes.
          </p>
          <button type="button" class="btn-primary text-sm" (click)="showForm.set(true)">
            Ajouter un type
          </button>
        </app-prime-card>
      } @else {
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          @for (t of types(); track t.id) {
            <app-prime-card className="ky-card--compact">
              <div class="flex items-start justify-between gap-2">
                <p class="font-medium text-primary">{{ t.label }}</p>
                @if (t.isActive) {
                  <span class="text-xs text-emerald-400">Actif</span>
                } @else {
                  <span class="text-xs text-muted">Inactif</span>
                }
              </div>
              <p class="text-muted text-sm">{{ t.code }} · {{ t.category }}</p>
              @if (t.defaultAmount) {
                <p class="text-xs mt-1 text-primary">Défaut : {{ t.defaultAmount | number:'1.0-0' }} MAD</p>
              }
              @if (t.maxAmount) {
                <p class="text-xs text-primary">Plafond : {{ t.maxAmount | number:'1.0-0' }} MAD</p>
              }
              @if (t.requiresJustification) {
                <p class="text-xs text-amber-400 mt-1">Motif obligatoire</p>
              }
            </app-prime-card>
          }
        </div>
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesCatalogPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly showForm = signal(false);
  readonly error = signal('');
  readonly formError = signal('');
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
    this.formError.set('');
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
      this.formError.set(e instanceof Error ? e.message : 'Erreur lors de la création.');
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.error.set('');
    try {
      this.types.set(await this.api.listTypes());
    } catch (e: unknown) {
      this.error.set(e instanceof Error ? e.message : 'Impossible de charger les types.');
    } finally {
      this.loading.set(false);
    }
  }
}
