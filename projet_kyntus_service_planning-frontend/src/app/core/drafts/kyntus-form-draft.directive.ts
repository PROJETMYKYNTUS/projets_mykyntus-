import { Directive, Input, OnDestroy, OnInit, inject } from '@angular/core';
import { FormGroupDirective } from '@angular/forms';
import { Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { KyntusFormDraftService } from './kyntus-form-draft.service';

/**
 * Persiste un FormGroup parent en sessionStorage et le restaure au chargement.
 * Usage : `<form [formGroup]="form" kyntusFormDraft="shift-config">`
 */
@Directive({
  selector: '[kyntusFormDraft]',
  standalone: true,
})
export class KyntusFormDraftDirective implements OnInit, OnDestroy {
  private readonly formGroupDir = inject(FormGroupDirective, { optional: true });
  private readonly drafts = inject(KyntusFormDraftService);

  @Input('kyntusFormDraft') draftKey = '';

  private sub: Subscription | null = null;
  private restoring = false;

  ngOnInit(): void {
    const group = this.formGroupDir?.form;
    if (!group || !this.draftKey) return;

    const saved = this.drafts.load<Record<string, unknown>>(this.draftKey);
    if (saved && typeof saved === 'object') {
      this.restoring = true;
      try {
        group.patchValue(saved, { emitEvent: false });
        group.markAsDirty();
      } finally {
        this.restoring = false;
      }
    }

    const flush = (): void => {
      if (this.restoring || !group.dirty) return;
      this.drafts.save(this.draftKey, group.getRawValue());
    };

    this.drafts.registerPendingFlush(this.draftKey, flush);

    this.sub = group.valueChanges.pipe(debounceTime(500)).subscribe(() => flush());
  }

  /** À appeler après submit réussi pour effacer le brouillon. */
  markSaved(): void {
    if (!this.draftKey) return;
    this.drafts.clear(this.draftKey);
    this.formGroupDir?.form.markAsPristine();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.sub = null;
    if (this.draftKey) {
      this.drafts.unregisterPendingFlush(this.draftKey);
    }
  }
}
