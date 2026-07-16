import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationNotificationService } from '../../../core/services/documentation-notification.service';
import type {
  CreateDocumentRequestPayload,
  DocumentTemplateListItemDto,
  DocumentTypeDto,
} from '../../../core/models/documentation.models';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { DocumentationApiService } from '../services/documentation-api.service';
import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import { formatDocumentationUxMessage } from '../../../core/lib/documentation-ux-messages';

const OTHER_KEY = '__autre__';
const TPL_PREFIX = 'tpl:';
const TYPE_PREFIX = 'type:';

@Component({
  standalone: true,
  selector: 'app-request-document-page',
  imports: [CommonModule, FormsModule, DocIconComponent],
  templateUrl: './request-document-page.component.html',
})
export class RequestDocumentPageComponent implements OnInit {
  readonly OTHER_KEY = OTHER_KEY;
  readonly otherLabel = 'Autre / hors catalogue';

  docTypes: DocumentTypeDto[] = [];
  /** Modèles actifs créés par les RH. */
  templates: DocumentTemplateListItemDto[] = [];
  docTypesLoading = false;
  docTypesError: string | null = null;

  /**
   * Sélection multi-documents : clés `tpl:{uuid}` ou `type:{uuid}`.
   * Mutuellement exclusive avec le mode « Autre ».
   */
  selectedKeys = new Set<string>();
  otherMode = false;

  otherDescription = '';
  otherDescriptionError = false;

  reason = '';
  complementaryComments = '';

  submitting = false;
  submitError: string | null = null;
  fieldValidationError: string | null = null;
  submitSuccess = false;
  submitSuccessCount = 0;
  submitSuccessRefs: string[] = [];
  submitPartialFailures: string[] = [];

  constructor(
    private readonly api: DocumentationApiService,
    private readonly data: DocumentationDataApiService,
    private readonly identity: DocumentationIdentityService,
    private readonly notify: DocumentationNotificationService,
  ) {}

  ngOnInit(): void {
    this.docTypesLoading = true;
    this.docTypesError = null;
    forkJoin({
      types: this.api.getDocTypesForCatalog(),
      templates: this.data.getDocumentTemplates(),
    }).subscribe({
      next: ({ types, templates }) => {
        this.docTypes = (types ?? []).filter((t) => t?.id);
        this.templates = (templates ?? []).filter((t) => t?.id && t.isActive);
        this.docTypesLoading = false;
        this.docTypesError = null;
        this.initDefaultSelection();
      },
      error: (err: unknown) => {
        this.docTypesLoading = false;
        this.docTypesError = this.formatHttpError(err);
        this.otherMode = true;
        this.selectedKeys.clear();
      },
    });
  }

  private initDefaultSelection(): void {
    this.selectedKeys.clear();
    this.otherMode = false;
    if (this.templates.length > 0) {
      this.selectedKeys.add(`${TPL_PREFIX}${this.templates[0]!.id}`);
    } else if (this.docTypes.length > 0) {
      this.selectedKeys.add(`${TYPE_PREFIX}${this.docTypes[0]!.id}`);
    } else {
      this.otherMode = true;
    }
  }

  isSelected(key: string): boolean {
    return this.selectedKeys.has(key);
  }

  toggleKey(key: string, checked: boolean): void {
    this.otherMode = false;
    this.otherDescriptionError = false;
    this.fieldValidationError = null;
    if (checked) this.selectedKeys.add(key);
    else this.selectedKeys.delete(key);
  }

  onOtherModeChange(enabled: boolean): void {
    this.otherMode = enabled;
    this.fieldValidationError = null;
    this.otherDescriptionError = false;
    if (enabled) this.selectedKeys.clear();
  }

  get selectionCount(): number {
    if (this.otherMode) return 1;
    return this.selectedKeys.size;
  }

  templateLabel(t: DocumentTemplateListItemDto): string {
    const type = t.documentTypeName?.trim();
    const kind = (t.kind ?? 'dynamic').toLowerCase() === 'static' ? ' (fichier prêt)' : '';
    return type ? `${t.name} — ${type}${kind}` : `${t.name}${kind}`;
  }

  handleSubmit(ev: Event): void {
    ev.preventDefault();
    this.submitError = null;
    this.submitSuccess = false;
    this.submitSuccessCount = 0;
    this.submitSuccessRefs = [];
    this.submitPartialFailures = [];
    this.fieldValidationError = null;

    if (this.docTypesLoading || this.submitting) return;

    if (this.otherMode) {
      const trimmed = this.otherDescription.trim();
      if (!trimmed) {
        this.otherDescriptionError = true;
        this.fieldValidationError = 'Veuillez décrire le document souhaité pour continuer.';
        return;
      }
      this.otherDescriptionError = false;
      this.postRequests([
        {
          isCustomType: true,
          documentTypeId: null,
          customTypeDescription: trimmed,
          reason: this.reason.trim() || null,
          complementaryComments: this.complementaryComments.trim() || null,
        },
      ]);
      return;
    }

    if (this.selectedKeys.size === 0) {
      this.fieldValidationError = 'Sélectionnez au moins un document (ou « Autre »).';
      return;
    }

    const payloads: CreateDocumentRequestPayload[] = [];
    for (const key of this.selectedKeys) {
      if (key.startsWith(TPL_PREFIX)) {
        const tid = key.slice(TPL_PREFIX.length).trim();
        const tpl = this.templates.find((t) => t.id === tid);
        if (!tpl) continue;
        const hasCatalogType = !!(tpl.documentTypeId ?? '').trim();
        payloads.push({
          isCustomType: !hasCatalogType,
          documentTypeId: hasCatalogType ? tpl.documentTypeId!.trim() : null,
          documentTemplateId: tid,
          customTypeDescription: null,
          reason: this.reason.trim() || null,
          complementaryComments: this.complementaryComments.trim() || null,
        });
      } else if (key.startsWith(TYPE_PREFIX)) {
        const typeId = key.slice(TYPE_PREFIX.length).trim();
        if (!typeId) continue;
        payloads.push({
          isCustomType: false,
          documentTypeId: typeId,
          reason: this.reason.trim() || null,
          complementaryComments: this.complementaryComments.trim() || null,
        });
      }
    }

    if (payloads.length === 0) {
      this.fieldValidationError = 'Sélection invalide. Réessayez.';
      return;
    }

    this.postRequests(payloads);
  }

  private postRequests(payloads: CreateDocumentRequestPayload[]): void {
    this.submitting = true;
    const calls = payloads.map((p) =>
      this.api.createDocumentRequest(p).pipe(
        catchError((err: unknown) => of({ __error: this.formatHttpError(err) } as const)),
      ),
    );

    forkJoin(calls)
      .pipe(finalize(() => (this.submitting = false)))
      .subscribe({
        next: (results) => {
          const refs: string[] = [];
          const failures: string[] = [];
          for (const r of results) {
            if (r && typeof r === 'object' && '__error' in r) {
              failures.push(String((r as { __error: string }).__error));
            } else if (r && typeof r === 'object' && 'id' in r) {
              refs.push(String((r as { id: string }).id));
            }
          }

          this.submitSuccessCount = refs.length;
          this.submitSuccessRefs = refs;
          this.submitPartialFailures = failures;

          if (refs.length > 0) {
            this.submitSuccess = true;
            this.reason = '';
            this.complementaryComments = '';
            this.otherDescription = '';
            this.otherDescriptionError = false;
            this.initDefaultSelection();
            const msg =
              failures.length === 0
                ? refs.length === 1
                  ? 'Votre demande a bien été envoyée.'
                  : `${refs.length} demandes ont bien été envoyées.`
                : `${refs.length} demande(s) envoyée(s), ${failures.length} échec(s).`;
            this.notify.showSuccess(msg);
            window.setTimeout(() => {
              this.submitSuccess = false;
            }, 5000);
          }

          if (failures.length > 0 && refs.length === 0) {
            this.submitError = failures[0] ?? 'Envoi impossible.';
            this.notify.showError(this.submitError);
          } else if (failures.length > 0) {
            this.submitError = failures.join(' · ');
          }
        },
        error: (err: unknown) => {
          this.submitError = this.formatHttpError(err);
          this.notify.showError(this.submitError);
        },
      });
  }

  private formatHttpError(err: unknown): string {
    return formatDocumentationUxMessage(
      err,
      'Votre demande n’a pas pu etre envoyee pour le moment. Merci de verifier les informations puis de reessayer.',
    );
  }
}
