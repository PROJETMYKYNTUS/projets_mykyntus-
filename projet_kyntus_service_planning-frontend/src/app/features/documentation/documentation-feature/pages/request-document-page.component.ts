import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';

import { DocumentationDataApiService } from '../../core/services/documentation-data-api.service';
import { DocumentationNotificationService } from '../../core/services/documentation-notification.service';
import type {
  CreateDocumentRequestPayload,
  DocumentTemplateDetailDto,
  DocumentTemplateListItemDto,
  DocumentTypeDto,
} from '../../shared/models/api.models';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { DocumentationApiService } from '../services/documentation-api.service';
import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import { formatDocumentationUxMessage } from '../../shared/utils/documentation-ux-messages';

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
   * Clé composite : `tpl:{uuid}` (modèle RH), `type:{uuid}` (type catalogue seul), ou `OTHER_KEY`.
   */
  selectionKey = '';

  otherDescription = '';
  otherDescriptionError = false;

  /** Détail du modèle sélectionné (variables / libellés). */
  templateDetail: DocumentTemplateDetailDto | null = null;
  templateDetailLoading = false;
  templateDetailError: string | null = null;
  /** Valeurs saisies par le pilote pour les variables du modèle. */
  pilotFieldValues: Record<string, string> = {};
  fieldValidationError: string | null = null;

  reason = '';
  complementaryComments = '';

  submitting = false;
  submitError: string | null = null;
  submitSuccess = false;
  submitSuccessRef: string | null = null;
  submitSuccessInternalId: string | null = null;
  submitSuccessTenant: string | null = null;

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
      types: this.api.getDocTypesForCatalog().pipe(catchError(() => of([] as DocumentTypeDto[]))),
      templates: this.data.getDocumentTemplates().pipe(catchError(() => of([] as DocumentTemplateListItemDto[]))),
    }).subscribe({
      next: ({ types, templates }) => {
        this.docTypes = (types ?? []).filter((t) => t?.id);
        this.templates = (templates ?? []).filter((t) => t?.id && t.isActive);
        this.docTypesLoading = false;
        this.initDefaultSelection();
      },
      error: (err: unknown) => {
        this.docTypesLoading = false;
        this.docTypesError = this.formatHttpError(err);
        this.selectionKey = OTHER_KEY;
      },
    });
  }

  private initDefaultSelection(): void {
    if (this.templates.length > 0) {
      const first = this.templates[0]!;
      this.selectionKey = `${TPL_PREFIX}${first.id}`;
      this.onSelectionChange(this.selectionKey);
    } else if (this.docTypes.length > 0) {
      this.selectionKey = `${TYPE_PREFIX}${this.docTypes[0]!.id}`;
      this.templateDetail = null;
      this.pilotFieldValues = {};
    } else {
      this.selectionKey = OTHER_KEY;
      this.templateDetail = null;
    }
  }

  onSelectionChange(value: string): void {
    this.selectionKey = value;
    this.otherDescriptionError = false;
    this.fieldValidationError = null;
    this.templateDetailError = null;
    if (value === OTHER_KEY) {
      this.otherDescription = '';
      this.templateDetail = null;
      this.pilotFieldValues = {};
      return;
    }
    if (value.startsWith(TYPE_PREFIX)) {
      this.templateDetail = null;
      this.pilotFieldValues = {};
      return;
    }
    if (value.startsWith(TPL_PREFIX)) {
      const id = value.slice(TPL_PREFIX.length).trim();
      if (id) this.loadTemplateDetail(id);
    }
  }

  private loadTemplateDetail(templateId: string): void {
    this.templateDetailLoading = true;
    this.templateDetail = null;
    this.pilotFieldValues = {};
    this.data.getDocumentTemplate(templateId).subscribe({
      next: (d) => {
        this.templateDetail = d;
        this.templateDetailLoading = false;
        this.pilotFieldValues = {};
        for (const v of this.pilotVariables(d)) {
          this.pilotFieldValues[v.name] = (v.defaultValue ?? '').trim();
        }
      },
      error: (err: unknown) => {
        this.templateDetailLoading = false;
        this.templateDetailError = this.formatHttpError(err);
      },
    });
  }

  /** Libellé affiché pour une variable (RH). */
  variableLabel(name: string, displayLabel?: string | null): string {
    const d = (displayLabel ?? '').trim();
    return d || name;
  }

  pilotVariables(detail: DocumentTemplateDetailDto | null = this.templateDetail): NonNullable<DocumentTemplateDetailDto['currentVersion']>['variables'] {
    return (detail?.currentVersion?.variables ?? []).filter((v) => {
      const scope = (v.formScope ?? 'pilot').toLowerCase();
      return scope !== 'hr' && scope !== 'db';
    });
  }

  handleSubmit(ev: Event): void {
    ev.preventDefault();
    this.submitError = null;
    this.submitSuccess = false;
    this.submitSuccessRef = null;
    this.submitSuccessInternalId = null;
    this.submitSuccessTenant = null;
    this.fieldValidationError = null;

    if (this.docTypesLoading || this.submitting) return;

    if (this.selectionKey.startsWith(TPL_PREFIX)) {
      if (this.templateDetailLoading || !this.templateDetail) {
        this.submitError = 'Chargement du modèle en cours ou indisponible.';
        return;
      }
      const tid = this.selectionKey.slice(TPL_PREFIX.length).trim();
      const vars = this.pilotVariables(this.templateDetail);
      for (const v of vars) {
        if (v.isRequired && !(this.pilotFieldValues[v.name] ?? '').trim()) {
          this.fieldValidationError = `Veuillez renseigner "${this.variableLabel(v.name, v.displayLabel)}".`;
          return;
        }
      }

      const hasCatalogType = !!(this.templateDetail.documentTypeId ?? '').trim();
      const payload: CreateDocumentRequestPayload = {
        isCustomType: !hasCatalogType,
        documentTypeId: hasCatalogType ? this.templateDetail.documentTypeId!.trim() : null,
        documentTemplateId: tid,
        customTypeDescription: null,
        reason: this.reason.trim() || null,
        complementaryComments: this.complementaryComments.trim() || null,
        initialFieldValues: { ...this.pilotFieldValues },
      };

      this.postRequest(payload);
      return;
    }

    if (this.selectionKey.startsWith(TYPE_PREFIX)) {
      const typeId = this.selectionKey.slice(TYPE_PREFIX.length).trim();
      if (!typeId) return;
      const payload: CreateDocumentRequestPayload = {
        isCustomType: false,
        documentTypeId: typeId,
        reason: this.reason.trim() || null,
        complementaryComments: this.complementaryComments.trim() || null,
      };
      this.postRequest(payload);
      return;
    }

    if (this.selectionKey === OTHER_KEY) {
      const trimmed = this.otherDescription.trim();
      if (!trimmed) {
        this.otherDescriptionError = true;
        this.fieldValidationError = 'Veuillez décrire le document souhaité pour continuer.';
        return;
      }
      this.otherDescriptionError = false;
      const payload: CreateDocumentRequestPayload = {
        isCustomType: true,
        documentTypeId: null,
        customTypeDescription: trimmed,
        reason: this.reason.trim() || null,
        complementaryComments: this.complementaryComments.trim() || null,
      };
      this.postRequest(payload);
    }
  }

  private postRequest(payload: CreateDocumentRequestPayload): void {
    this.submitting = true;
    this.api
      .createDocumentRequest(payload)
      .pipe(finalize(() => (this.submitting = false)))
      .subscribe({
        next: (created) => {
          this.submitSuccess = true;
          this.submitSuccessRef = created.id;
          this.submitSuccessInternalId = created.internalId;
          this.submitSuccessTenant = this.identity.getTenantId() || null;
          this.reason = '';
          this.complementaryComments = '';
          this.otherDescription = '';
          this.otherDescriptionError = false;
          this.pilotFieldValues = {};
          this.initDefaultSelection();
          this.notify.showSuccess('Votre demande a bien ete envoyee.');
          window.setTimeout(() => {
            this.submitSuccess = false;
          }, 4000);
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
