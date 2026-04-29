import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import mammoth from 'mammoth';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationNotificationService } from '../../../core/services/documentation-notification.service';
import type {
  DocumentTemplateDetailDto,
  DocumentTemplateListItemDto,
  TemplateVariableDto,
} from '../../../shared/models/api.models';
import { formatDocumentationUxMessage } from '../../../shared/utils/documentation-ux-messages';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';

@Component({
  standalone: true,
  selector: 'app-templates-page',
  imports: [CommonModule, FormsModule, DocIconComponent],
  templateUrl: './templates-page.component.html',
  styles: [`
    .template-action-row {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(10.25rem, 1fr));
      gap: 0.5rem;
      align-items: stretch;
    }

    .template-action-button {
      position: relative;
      box-sizing: border-box;
      display: inline-flex;
      width: 100%;
      min-width: 0;
      min-height: 2.875rem;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.65rem 0.85rem;
      border-radius: 0.5rem;
      border: 1px solid transparent;
      font-size: 0.8125rem;
      font-weight: 600;
      line-height: 1.3;
      text-align: center;
      white-space: normal;
      word-break: break-word;
      transition:
        transform 160ms ease,
        box-shadow 160ms ease,
        filter 160ms ease,
        opacity 160ms ease,
        background-color 160ms ease,
        border-color 160ms ease;
    }

    .template-action-button:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 8px 16px rgba(15, 23, 42, 0.25);
      filter: brightness(1.04);
    }

    .template-action-button:disabled {
      pointer-events: none;
      opacity: 0.55;
    }

    .template-action-button--icon {
      min-width: 2.875rem;
      max-width: 3.25rem;
      padding-left: 0.5rem;
      padding-right: 0.5rem;
    }

    .template-action-button--icon .template-action-button__label {
      gap: 0;
    }

    .template-action-button__label {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.45rem;
      min-width: 0;
      max-width: 100%;
    }

    .template-action-button__spinner {
      height: 1rem;
      width: 1rem;
      flex: 0 0 auto;
      border: 2px solid currentColor;
      border-right-color: transparent;
      border-radius: 9999px;
      animation: template-action-spin 0.75s linear infinite;
    }

    @keyframes template-action-spin {
      to {
        transform: rotate(360deg);
      }
    }
  `],
})
export class TemplatesPageComponent implements OnInit, OnDestroy {
  /** Exemple dâ€™accolades affichÃ© tel quel (Ã©viter {{ â€¦ }} dans le HTML, le compilateur Angular les interprÃ¨te). */
  readonly placeholderSyntaxExample = '{{nom}}';
  templates: DocumentTemplateListItemDto[] = [];
  selectedTemplate: DocumentTemplateDetailDto | null = null;
  loading = true;
  error: string | null = null;
  selectedTemplateId: string | null = null;
  lastMessage: string | null = null;
  uploadFile: File | null = null;
  form = {
    code: '',
    name: '',
    documentTypeId: '',
    fileName: '',
    content: '',
    description: '',
  };
  /** DonnÃ©es fictives pour Â« Tester template Â» â€” gÃ©nÃ©rÃ© Ã  partir des variables du modÃ¨le sÃ©lectionnÃ©, pas extrait du Word. */
  sampleDataRaw = '{}';
  testRunRendered: string | null = null;
  missingVariables: string[] = [];

  /** Modale prÃ©visualisation (PDF natif, DOCX â†’ HTML via mammoth). */
  previewOpen = false;
  previewLoading = false;
  previewTitle = '';
  previewKind: 'pdf' | 'docx' | 'other' | null = null;
  previewPdfSafeUrl: SafeResourceUrl | null = null;
  previewDocxHtml: SafeHtml | null = null;
  previewFileName: string | null = null;
  /** URL blob pour iframe PDF / lien TÃ©lÃ©charger â€” public pour le template. */
  previewBlobUrl: string | null = null;
  cleaningDrafts = false;
  creatingTemplate = false;
  private readonly templateActionLoading = new Map<string, TemplateAction>();

  private sub = new Subscription();

  constructor(
    private readonly data: DocumentationDataApiService,
    private readonly sanitizer: DomSanitizer,
    private readonly notify: DocumentationNotificationService,
  ) {}

  ngOnInit(): void {
    this.reloadTemplates();
  }

  private reloadTemplates(afterLoaded?: () => void): void {
    this.loading = true;
    this.sub.add(
      this.data.getDocumentTemplates().subscribe({
        next: (rows) => {
          this.templates = rows;
          this.loading = false;
          this.error = null;
          afterLoaded?.();
        },
        error: () => {
          this.templates = [];
          this.loading = false;
          this.error = 'Impossible de charger les modÃ¨les (API /api/documentation/data/document-templates).';
        },
      }),
    );
  }

  /** Couleur de la banniÃ¨re selon le libellÃ© (sans toucher Ã  la logique mÃ©tier des messages). */
  get bannerToneComputed(): 'success' | 'error' | 'info' {
    const raw = this.lastMessage ?? '';
    if (!raw.trim()) return 'success';
    const m = raw.toLowerCase();
    if (
      m.includes('Ã©chec') ||
      m.includes('impossible') ||
      m.includes('refus') ||
      m.includes('introuvable') ||
      m.includes('bloquÃ©')
    ) {
      return 'error';
    }
    if (
      m.includes('obligatoire') ||
      m.includes('choisissez un fichier') ||
      m.includes('choisissez un') ||
      m.includes('activez le modÃ¨le') ||
      m.includes('renseignez') ||
      m.includes('nom technique') ||
      m.includes('json de donnÃ©es') ||
      m.includes('double') ||
      m.includes('invalide')
    ) {
      return 'info';
    }
    return 'success';
  }

  ngOnDestroy(): void {
    this.closePreview();
    this.sub.unsubscribe();
  }

  generate(t: DocumentTemplateListItemDto): void {
    if (this.isTemplateActionLoading(t.id)) return;
    if (!t.isActive) {
      this.lastMessage = 'Activez le modÃ¨le (bouton Activer) avant de gÃ©nÃ©rer un PDF.';
      return;
    }
    this.setTemplateActionLoading(t.id, 'generate');
    this.lastMessage = null;
    const sample = this.parseSampleData() ?? {};
    const body: { documentTypeId?: string; variables: Record<string, string> } = { variables: sample };
    if (t.documentTypeId?.trim()) body.documentTypeId = t.documentTypeId.trim();
    this.sub.add(
      this.data.generateFromDocumentTemplate(t.id, body).subscribe({
        next: (res) => {
          this.clearTemplateActionLoading(t.id);
          this.lastMessage = `GÃ©nÃ©rÃ© : ${res.fileName} â€” ${res.storageUri}`;
          this.notify.showSuccess('Document gÃ©nÃ©rÃ© avec succÃ¨s.');
          this.reloadTemplates();
        },
        error: (err) => {
          this.clearTemplateActionLoading(t.id);
          this.lastMessage = this.apiErrorMessage(
            err,
            'Ã‰chec de la gÃ©nÃ©ration (vÃ©rifiez les en-tÃªtes et la session).',
          );
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  createTemplate(): void {
    if (this.creatingTemplate) return;
    this.lastMessage = null;
    if (!this.form.name.trim()) {
      this.lastMessage = 'Le nom est obligatoire.';
      return;
    }

    const effectiveCode = this.resolveTemplateCode();
    if (!effectiveCode) {
      this.lastMessage = 'Impossible de gÃ©nÃ©rer un code template. Renseignez le nom ou le code.';
      return;
    }

    const documentTypeId = this.form.documentTypeId.trim() || null;

    if (this.uploadFile) {
      this.creatingTemplate = true;
      this.sub.add(
        this.data
          .createTemplateFromUploadFile({
            code: effectiveCode,
            name: this.form.name.trim(),
            description: null,
            documentTypeId,
            file: this.uploadFile,
            kind: 'dynamic',
          })
          .subscribe({
            next: (res) => {
              this.creatingTemplate = false;
              this.lastMessage = `Le modÃ¨le Â« ${res.name} Â» a Ã©tÃ© crÃ©Ã© avec succÃ¨s. Ouvrez le dÃ©tail pour vÃ©rifier les formulaires Pilote / RH.`;
              this.notify.showSuccess('ModÃ¨le crÃ©Ã©. VÃ©rifiez les formulaires dans le dÃ©tail du modÃ¨le.');
              this.clearForm();
              this.reloadTemplates(() => this.selectTemplate(res.id));
            },
            error: (err) => {
              this.creatingTemplate = false;
              this.lastMessage = this.apiErrorMessage(
                err,
                "Ã‰chec de l'import du modÃ¨le. VÃ©rifiez le stockage des fichiers puis rÃ©essayez.",
              );
              this.notify.showError(this.lastMessage);
            },
          }),
      );
      return;
    }
    this.lastMessage = 'Choisissez un fichier modÃ¨le (PDF ou DOCX) via Â« Importer Â».';
  }

  selectTemplate(templateId: string, afterLoad?: () => void): void {
    if (this.isTemplateActionLoading(templateId)) return;
    this.setTemplateActionLoading(templateId, 'detail');
    this.selectedTemplateId = templateId;
    this.testRunRendered = null;
    this.missingVariables = [];
    this.sub.add(
      this.data.getDocumentTemplate(templateId).subscribe({
        next: (res) => {
          this.clearTemplateActionLoading(templateId);
          this.selectedTemplate = this.normalizeTemplateDetailScopes(res);
          this.sampleDataRaw = this.buildSampleJsonFromVariables(this.selectedTemplate.currentVersion?.variables ?? []);
          window.setTimeout(() => {
            const el = document.getElementById('template-detail-panel');
            if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }, 0);
          afterLoad?.();
        },
        error: (err) => {
          this.clearTemplateActionLoading(templateId);
          this.lastMessage = 'Impossible de charger le dÃ©tail du template.';
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  visualizeTemplate(t: DocumentTemplateListItemDto): void {
    if (this.isTemplateActionLoading(t.id)) return;
    this.setTemplateActionLoading(t.id, 'visualize');
    this.openTemplatePreview(t.id, t.name);
  }

  downloadTemplateSource(t: DocumentTemplateListItemDto): void {
    if (this.isTemplateActionLoading(t.id)) return;
    this.setTemplateActionLoading(t.id, 'downloadSource');
    this.sub.add(
      this.data.getTemplateSourceFileBlob(t.id).subscribe({
        next: (resp) => {
          this.clearTemplateActionLoading(t.id);
          const blob = resp.body;
          if (!blob?.size) {
            this.lastMessage = 'Fichier vide ou introuvable.';
            this.notify.showError(this.lastMessage);
            return;
          }
          const fn =
            this.fileNameFromContentDisposition(resp.headers.get('content-disposition')) ??
            `${(t.name || 'modele').replace(/[/\\?%*:|"<>]/g, '_')}`;
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = fn;
          a.rel = 'noopener';
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          URL.revokeObjectURL(url);
          this.notify.showSuccess('TÃ©lÃ©chargement dÃ©marrÃ©.');
        },
        error: (err: HttpErrorResponse) => {
          this.clearTemplateActionLoading(t.id);
          this.lastMessage = this.apiErrorMessage(err, 'Ã‰chec du tÃ©lÃ©chargement du modÃ¨le.');
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  openSelectedTemplateFile(): void {
    if (!this.selectedTemplate) return;
    this.openTemplatePreview(this.selectedTemplate.id, this.selectedTemplate.name);
  }

  closePreview(): void {
    this.previewOpen = false;
    this.previewLoading = false;
    this.previewKind = null;
    this.previewPdfSafeUrl = null;
    this.previewDocxHtml = null;
    this.previewFileName = null;
    if (this.previewBlobUrl) {
      URL.revokeObjectURL(this.previewBlobUrl);
      this.previewBlobUrl = null;
    }
    for (const [templateId, action] of this.templateActionLoading.entries()) {
      if (action === 'visualize' || action === 'downloadSource') {
        this.templateActionLoading.delete(templateId);
      }
    }
  }

  private openTemplatePreview(templateId: string, title: string): void {
    this.lastMessage = null;
    this.closePreview();
    this.previewOpen = true;
    this.previewLoading = true;
    this.previewTitle = title;

    this.sub.add(
      this.data.getTemplateSourceFileBlob(templateId).subscribe({
        next: (resp) => {
          const blob = resp.body;
          if (!blob || blob.size === 0) {
            this.previewLoading = false;
            this.previewOpen = false;
            this.clearTemplateActionLoading(templateId);
            this.lastMessage = 'Fichier vide ou introuvable.';
            this.notify.showError(this.lastMessage);
            return;
          }
          const headerCt = resp.headers.get('content-type')?.split(';')[0]?.trim() ?? '';
          const fn = this.fileNameFromContentDisposition(resp.headers.get('content-disposition')) ?? 'document';
          this.previewFileName = fn;
          void this.applyPreviewBlob(templateId, blob, headerCt, fn);
        },
        error: (err: HttpErrorResponse) => {
          void this.handlePreviewHttpError(err).then((msg) => {
            this.previewLoading = false;
            this.previewOpen = false;
            this.lastMessage = msg;
            this.notify.showError(msg);
            this.clearTemplateActionLoading(templateId);
          });
        },
      }),
    );
  }

  private async applyPreviewBlob(
    templateId: string,
    blob: Blob,
    headerContentType: string,
    fileName: string,
  ): Promise<void> {
    const lower = fileName.toLowerCase();
    const ct = headerContentType.toLowerCase();

    if (ct === 'application/pdf' || lower.endsWith('.pdf')) {
      const url = URL.createObjectURL(blob);
      this.previewBlobUrl = url;
      this.previewPdfSafeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
      this.previewKind = 'pdf';
      this.previewLoading = false;
      this.clearTemplateActionLoading(templateId);
      return;
    }

    if (
      ct.includes('wordprocessingml') ||
      ct === 'application/msword' ||
      lower.endsWith('.docx')
    ) {
      try {
        const buf = await blob.arrayBuffer();
        const result = await mammoth.convertToHtml({ arrayBuffer: buf });
        this.previewDocxHtml = this.sanitizer.bypassSecurityTrustHtml(result.value);
        this.previewKind = 'docx';
      } catch {
        const url = URL.createObjectURL(blob);
        this.previewBlobUrl = url;
        this.previewKind = 'other';
      }
      this.previewLoading = false;
      this.clearTemplateActionLoading(templateId);
      return;
    }

    const url = URL.createObjectURL(blob);
    this.previewBlobUrl = url;
    this.previewKind = 'other';
    this.previewLoading = false;
    this.clearTemplateActionLoading(templateId);
  }

  private async handlePreviewHttpError(err: HttpErrorResponse): Promise<string> {
    if (err.error instanceof Blob) {
      try {
        const t = await err.error.text();
        const j = JSON.parse(t) as { message?: unknown };
        if (typeof j.message === 'string' && j.message.trim()) return j.message.trim();
      } catch {
        /* ignore */
      }
    }
    return this.apiErrorMessage(err, 'Impossible de charger le fichier pour prÃ©visualisation.');
  }

  private fileNameFromContentDisposition(cd: string | null): string | null {
    if (!cd) return null;
    const star = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(cd);
    if (star?.[1]) {
      try {
        return decodeURIComponent(star[1].trim().replace(/(^")|("$)/g, ''));
      } catch {
        return star[1].trim();
      }
    }
    const fn = /filename\s*=\s*"([^"]+)"/i.exec(cd);
    if (fn?.[1]) return fn[1];
    const fn2 = /filename\s*=\s*([^;\s]+)/i.exec(cd);
    return fn2?.[1]?.trim() ?? null;
  }

  /** AperÃ§u du texte issu de lâ€™analyse DOCX (champ bodyText du JSON stockÃ© cÃ´tÃ© API). */
  analyzedTextExcerpt(): string | null {
    const raw = this.selectedTemplate?.currentVersion?.structuredContent;
    if (!raw?.trim()) return null;
    try {
      const o = JSON.parse(raw) as { format?: string; bodyText?: string };
      if (typeof o.bodyText === 'string' && o.bodyText.trim()) {
        const t = o.bodyText.trim();
        return t.length > 2500 ? `${t.slice(0, 2500)}â€¦` : t;
      }
    } catch {
      /* ignore */
    }
    return null;
  }

  private buildSampleJsonFromVariables(vars: TemplateVariableDto[]): string {
    if (vars.length === 0) {
      return '{}';
    }
    const o: Record<string, string> = {};
    for (const v of vars) {
      o[v.name] = '';
    }
    return `${JSON.stringify(o, null, 2)}`;
  }

  toggleTemplateStatus(template: DocumentTemplateListItemDto): void {
    if (this.isTemplateActionLoading(template.id)) return;
    this.setTemplateActionLoading(template.id, 'toggle');
    this.sub.add(
      this.data.setTemplateStatus(template.id, !template.isActive).subscribe({
        next: () => {
          this.clearTemplateActionLoading(template.id);
          this.lastMessage = `Template ${template.code} ${template.isActive ? 'dÃ©sactivÃ©' : 'activÃ©'}.`;
          this.notify.showSuccess(template.isActive ? 'ModÃ¨le dÃ©sactivÃ©.' : 'ModÃ¨le activÃ©.');
          this.reloadTemplates();
          if (this.selectedTemplateId === template.id) this.selectTemplate(template.id);
        },
        error: () => {
          this.clearTemplateActionLoading(template.id);
          this.lastMessage = 'Ã‰chec mise Ã  jour du statut.';
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  pilotVarBusy = false;

  addPilotVariable(formScope: 'pilot' | 'hr' | 'db' = 'pilot'): void {
    const vars = this.selectedTemplate?.currentVersion?.variables;
    if (!vars) return;
    const used = new Set(vars.map((v) => v.name.trim().toLowerCase()));
    let idx = vars.length + 1;
    let name = `champ_${idx}`;
    while (used.has(name.toLowerCase())) {
      idx += 1;
      name = `champ_${idx}`;
    }
    vars.push({
      id: `tmp-${Date.now()}-${idx}`,
      name,
      type: 'text',
      isRequired: false,
      defaultValue: null,
      validationRule: null,
      displayLabel: '',
      formScope,
      sourcePriority: formScope === 'hr' ? 30 : formScope === 'db' ? 10 : 20,
      normalizedName: name,
      rawPlaceholder: formScope === 'hr' ? null : `(${name})`,
      sortOrder: vars.length,
    });
  }

  removePilotVariable(index: number): void {
    const vars = this.selectedTemplate?.currentVersion?.variables;
    if (!vars) return;
    if (index < 0 || index >= vars.length) return;
    vars.splice(index, 1);
    vars.forEach((v, i) => (v.sortOrder = i));
  }

  private normalizeFormScope(scope: string | null | undefined): 'pilot' | 'hr' | 'db' {
    const normalized = (scope ?? 'pilot').trim().toLowerCase();
    if (normalized === 'hr' || normalized === 'both') return 'hr';
    if (normalized === 'db') return 'db';
    return 'pilot';
  }

  private normalizeTemplateVariableScopes(vars: TemplateVariableDto[]): TemplateVariableDto[] {
    return vars.map((v) => {
      const formScope = this.normalizeFormScope(v.formScope);
      return {
        ...v,
        formScope,
        sourcePriority: formScope === 'hr' ? 30 : formScope === 'db' ? 10 : 20,
      };
    });
  }

  /** ClÃ© stable pour @for / ngModel : Ã©vite les mÃ©langes de lignes quand formScope dÃ©place une variable entre listes. */
  private ensureTemplateVariableIds(vars: TemplateVariableDto[]): TemplateVariableDto[] {
    return vars.map((v, i) => {
      const trimmed = v.id?.trim();
      if (trimmed) return { ...v, id: trimmed };
      const n = (v.name ?? '').trim() || `var${i}`;
      const slug = n.replace(/[^a-zA-Z0-9_-]/g, '_');
      return { ...v, id: `local-${i}-${slug}` };
    });
  }

  private normalizeTemplateDetailScopes(detail: DocumentTemplateDetailDto): DocumentTemplateDetailDto {
    if (!detail.currentVersion?.variables?.length) return detail;
    return {
      ...detail,
      currentVersion: {
        ...detail.currentVersion,
        variables: this.ensureTemplateVariableIds(
          this.normalizeTemplateVariableScopes(detail.currentVersion.variables),
        ),
      },
    };
  }

  pilotVariables(): TemplateVariableDto[] {
    return (this.selectedTemplate?.currentVersion?.variables ?? []).filter(
      (v) => this.normalizeFormScope(v.formScope) === 'pilot',
    );
  }

  hrVariables(): TemplateVariableDto[] {
    return (this.selectedTemplate?.currentVersion?.variables ?? []).filter(
      (v) => this.normalizeFormScope(v.formScope) === 'hr',
    );
  }

  dbVariables(): TemplateVariableDto[] {
    return (this.selectedTemplate?.currentVersion?.variables ?? []).filter(
      (v) => this.normalizeFormScope(v.formScope) === 'db',
    );
  }

  /** Enregistre la dÃ©finition des formulaires Pilote / RH / DB (version courante). */
  savePilotDefinitions(): void {
    if (!this.selectedTemplate?.currentVersion?.variables?.length) {
      this.lastMessage = 'Aucune variable Ã  enregistrer.';
      return;
    }
    const names = new Set<string>();
    for (const v of this.selectedTemplate.currentVersion.variables) {
      const raw = v.name.trim();
      if (!raw) {
        this.lastMessage = 'Chaque donnÃ©e nÃ©cessaire doit avoir un nom technique (ex: cin, rib).';
        return;
      }
      const normalized = raw.toLowerCase();
      if (names.has(normalized)) {
        this.lastMessage = `Nom de variable en double: ${raw}`;
        return;
      }
      names.add(normalized);
    }
    const vars = this.selectedTemplate.currentVersion.variables.map((v) => {
      const formScope = this.normalizeFormScope(v.formScope);
      return {
        name: v.name.trim(),
        type: v.type,
        isRequired: v.isRequired,
        defaultValue: v.defaultValue,
        validationRule: v.validationRule,
        displayLabel: v.displayLabel,
        formScope,
        sourcePriority: v.sourcePriority ?? (formScope === 'hr' ? 30 : formScope === 'db' ? 10 : 20),
        normalizedName: v.normalizedName ?? v.name.trim(),
        rawPlaceholder: v.rawPlaceholder ?? null,
      };
    });
    this.pilotVarBusy = true;
    this.sub.add(
      this.data.putCurrentVersionTemplateVariables(this.selectedTemplate.id, vars).subscribe({
        next: (res) => {
          this.selectedTemplate = this.normalizeTemplateDetailScopes(res);
          this.pilotVarBusy = false;
          this.lastMessage = 'Formulaires Pilote / RH / DB enregistrÃ©s.';
          this.sampleDataRaw = this.buildSampleJsonFromVariables(this.selectedTemplate.currentVersion?.variables ?? []);
          this.notify.showSuccess('Formulaires enregistrÃ©s.');
        },
        error: (err) => {
          this.pilotVarBusy = false;
          this.lastMessage = this.apiErrorMessage(err, 'Ã‰chec enregistrement des formulaires.');
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  publishNewVersion(): void {
    if (!this.selectedTemplate) return;
    const currentVersion = this.selectedTemplate.currentVersion;
    const content = currentVersion?.structuredContent ?? '';
    const vars: TemplateVariableDto[] = currentVersion?.variables ?? [];
    this.sub.add(
      this.data
        .createTemplateVersion(this.selectedTemplate.id, {
          structuredContent: content,
          status: 'published',
          originalAssetUri: currentVersion?.originalAssetUri ?? null,
          variables: vars.map((v) => {
            const formScope = this.normalizeFormScope(v.formScope);
            return {
              name: v.name.trim(),
              type: v.type,
              isRequired: v.isRequired,
              defaultValue: v.defaultValue,
              validationRule: v.validationRule,
              displayLabel: v.displayLabel,
              formScope,
              sourcePriority: v.sourcePriority ?? (formScope === 'hr' ? 30 : formScope === 'db' ? 10 : 20),
              normalizedName: v.normalizedName ?? v.name.trim(),
              rawPlaceholder: v.rawPlaceholder ?? null,
            };
          }),
        })
        .subscribe({
          next: (res) => {
            this.lastMessage = `Version ${res.versionNumber} publiÃ©e.`;
            this.selectTemplate(this.selectedTemplate!.id);
            this.reloadTemplates();
          },
          error: (err) => {
            this.lastMessage = this.apiErrorMessage(err, 'Ã‰chec publication version.');
          },
        }),
    );
  }

  runTest(): void {
    if (!this.selectedTemplate) return;
    const sample = this.parseSampleData();
    if (!sample) {
      this.lastMessage = 'JSON de donnÃ©es fictives invalide.';
      return;
    }
    this.sub.add(
      this.data.testRunTemplate(this.selectedTemplate.id, sample).subscribe({
        next: (res) => {
          this.testRunRendered = res.renderedContent;
          this.missingVariables = res.missingVariables;
        },
        error: () => (this.lastMessage = 'Ã‰chec test-run template.'),
      }),
    );
  }

  private parseSampleData(): Record<string, string> | null {
    try {
      const raw = JSON.parse(this.sampleDataRaw) as Record<string, unknown>;
      const normalized: Record<string, string> = {};
      Object.keys(raw).forEach((k) => {
        normalized[k] = String(raw[k] ?? '');
      });
      return normalized;
    } catch {
      return null;
    }
  }

  onUploadFileSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const f = input.files?.[0];
    this.uploadFile = f ?? null;
  }

  onNameChanged(value: string): void {
    if (this.form.code.trim()) return;
    this.form.code = this.buildTemplateCodeFromName(value);
  }

  deleteTemplate(template: DocumentTemplateListItemDto): void {
    if (this.isTemplateActionLoading(template.id)) return;
    const ok = window.confirm(`Supprimer le template Â« ${template.name} Â» ?`);
    if (!ok) return;
    this.setTemplateActionLoading(template.id, 'delete');
    this.sub.add(
      this.data.deleteTemplate(template.id).subscribe({
        next: () => {
          this.clearTemplateActionLoading(template.id);
          if (this.selectedTemplateId === template.id) {
            this.selectedTemplateId = null;
            this.selectedTemplate = null;
          }
          this.lastMessage = `Template supprimÃ© : ${template.code}`;
          this.notify.showSuccess('ModÃ¨le supprimÃ©.');
          this.reloadTemplates();
        },
        error: (err) => {
          this.clearTemplateActionLoading(template.id);
          this.lastMessage = this.apiErrorMessage(err, 'Suppression refusÃ©e.');
          this.notify.showError(this.lastMessage);
        },
      }),
    );
  }

  cleanupDraftTemplates(): void {
    if (this.cleaningDrafts) return;
    const candidates = this.templates.filter((t) => !t.isActive);
    if (candidates.length === 0) {
      this.lastMessage = 'Aucun template inactif Ã  nettoyer.';
      return;
    }
    const ok = window.confirm(
      `Nettoyer ${candidates.length} template(s) inactif(s) ? Les demandes actives empÃªcheront la suppression.`,
    );
    if (!ok) return;
    this.cleaningDrafts = true;
    const jobs = candidates.map((t) =>
      this.data.deleteTemplate(t.id).pipe(
        map(() => ({ ok: true as const, code: t.code })),
        catchError((err) =>
          of({
            ok: false as const,
            code: t.code,
            reason: this.apiErrorMessage(err, 'Suppression refusÃ©e'),
          }),
        ),
      ),
    );
    this.sub.add(
      forkJoin(jobs).subscribe({
        next: (results) => {
          this.cleaningDrafts = false;
          const success = results.filter((r) => r.ok).length;
          const fails = results.filter((r) => !r.ok);
          if (fails.length === 0) {
            this.lastMessage = `Nettoyage terminÃ© : ${success} template(s) supprimÃ©(s).`;
          } else {
            const sample = fails.slice(0, 3).map((f) => `${f.code}: ${f.reason}`).join(' | ');
            this.lastMessage = `Nettoyage partiel : ${success} supprimÃ©(s), ${fails.length} bloquÃ©(s). ${sample}`;
          }
          this.reloadTemplates();
        },
        error: () => {
          this.cleaningDrafts = false;
          this.lastMessage = 'Ã‰chec du nettoyage des brouillons.';
        },
      }),
    );
  }

  /** Affiche le champ message du JSON dâ€™erreur API (503 MinIO, 409 code dupliquÃ©, etc.). */
  private apiErrorMessage(err: unknown, fallback: string): string {
    return formatDocumentationUxMessage(err, fallback);
  }

  private clearForm(): void {
    this.uploadFile = null;
    this.form = { code: '', name: '', documentTypeId: '', fileName: '', content: '', description: '' };
  }

  isTemplateActionLoading(templateId: string, action?: TemplateAction): boolean {
    const current = this.templateActionLoading.get(templateId);
    return action ? current === action : !!current;
  }

  private setTemplateActionLoading(templateId: string, action: TemplateAction): void {
    this.templateActionLoading.set(templateId, action);
  }

  private clearTemplateActionLoading(templateId: string): void {
    this.templateActionLoading.delete(templateId);
  }

  private resolveTemplateCode(): string {
    const explicit = this.form.code.trim().toUpperCase();
    if (explicit) return explicit;
    const generated = this.buildTemplateCodeFromName(this.form.name);
    this.form.code = generated;
    return generated;
  }

  private buildTemplateCodeFromName(name: string): string {
    const base = name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toUpperCase()
      .replace(/[^A-Z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '')
      .slice(0, 44);
    const stamp = new Date().toISOString().replace(/[-:TZ.]/g, '').slice(2, 14);
    return (base ? `${base}_${stamp}` : `TEMPLATE_${stamp}`).slice(0, 64);
  }
}

type TemplateAction = 'detail' | 'visualize' | 'downloadSource' | 'toggle' | 'delete' | 'generate';
